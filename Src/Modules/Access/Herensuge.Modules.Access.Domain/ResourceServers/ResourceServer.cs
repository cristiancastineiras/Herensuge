using System.Text.RegularExpressions;
using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers.Events;

namespace Herensuge.Modules.Access.Domain.ResourceServers;

/// <summary>
/// An API of the SaaS protected by Herensuge: its permission catalog and role templates come from its
/// manifest. Permissions are never deleted while they may be in use: they are deprecated and grant nothing.
/// </summary>
public sealed partial class ResourceServer : AggregateRoot
{
    public const int MaxPermissions = 500;
    public const int MaxRoleTemplates = 50;

    private readonly List<Permission> _permissions = [];
    private readonly List<RoleTemplate> _roleTemplates = [];

    private ResourceServer()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>"facturas": scope "facturas.api", audience "facturas-api".</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>OIDC client of the app: the only one allowed to ask for decisions and to sync the manifest.</summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>Whether the permissions travel in the access token (false for large catalogs: the API asks the PDP).</summary>
    public bool EmbedPermissions { get; private set; }

    public int ManifestVersion { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Permission> Permissions => _permissions;

    public IReadOnlyCollection<RoleTemplate> RoleTemplates => _roleTemplates;

    public string Scope => ScopeFor(Key);

    public string Audience => AudienceFor(Key);

    public IEnumerable<Permission> ActivePermissions => _permissions.Where(permission => permission.IsActive);

    public static string ScopeFor(string key) => $"{key}.api";

    public static string AudienceFor(string key) => $"{key}-api";

    public static Result<ResourceServer> Register(AccessManifest manifest, DateTime utcNow)
    {
        var server = new ResourceServer { Id = Guid.CreateVersion7(), Key = manifest.ResourceServer, CreatedAtUtc = utcNow };
        Result applied = server.ApplyManifest(manifest, utcNow);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        return server;
    }

    public Result ApplyManifest(AccessManifest manifest, DateTime utcNow)
    {
        // The manifest arrives as JSON: missing lists are empty lists.
        manifest = manifest with
        {
            Permissions = manifest.Permissions ?? [],
            Roles = [.. (manifest.Roles ?? []).Select(role => role with { Permissions = role.Permissions ?? [] })],
        };

        Result valid = Validate(manifest);
        if (valid.IsFailure)
        {
            return valid;
        }

        if (manifest.ResourceServer != Key)
        {
            return AccessErrors.InvalidManifest("The resource server key cannot change.");
        }

        Name = manifest.Name.Trim();
        ClientId = manifest.ClientId;
        EmbedPermissions = manifest.EmbedPermissions ?? true;

        var declared = manifest.Permissions.ToDictionary(permission => permission.Key, StringComparer.Ordinal);
        foreach (Permission existing in _permissions)
        {
            if (declared.Remove(existing.Key, out ManifestPermission? permission))
            {
                existing.Update(permission.Description, permission.Requires ?? PermissionRequirements.None);
            }
            else
            {
                existing.Deprecate(utcNow);
            }
        }

        foreach (ManifestPermission permission in manifest.Permissions.Where(permission => declared.ContainsKey(permission.Key)))
        {
            _permissions.Add(Permission.Create(Id, permission.Key, permission.Description, permission.Requires ?? PermissionRequirements.None));
        }

        string[] active = [.. ActivePermissions.Select(permission => permission.Key)];
        var templates = manifest.Roles.ToDictionary(role => role.Key, StringComparer.Ordinal);
        _roleTemplates.RemoveAll(template => !templates.ContainsKey(template.Key));
        foreach (ManifestRole role in manifest.Roles)
        {
            string[] permissions = Expand(role.Permissions, active);
            string[] grantTo = [.. (role.GrantTo ?? []).Distinct(StringComparer.Ordinal)];
            RoleTemplate? template = _roleTemplates.FirstOrDefault(candidate => candidate.Key == role.Key);
            if (template is null)
            {
                _roleTemplates.Add(RoleTemplate.Create(Id, role.Key, role.Name, role.Description, permissions, grantTo));
            }
            else
            {
                template.Update(role.Name, role.Description, permissions, grantTo);
            }
        }

        ManifestVersion++;
        UpdatedAtUtc = utcNow;
        Raise(new ResourceServerSyncedDomainEvent(Id, Key, Scope, Audience, ClientId, ManifestVersion));
        return Result.Success();
    }

    /// <summary>"invoice:*" → every active invoice permission; "*" → all of them. Unknown keys were rejected by Validate.</summary>
    private static string[] Expand(IReadOnlyList<string> patterns, string[] active) =>
    [
        .. patterns
            .SelectMany(pattern => pattern switch
            {
                "*" => active,
                _ when pattern.EndsWith(":*", StringComparison.Ordinal) => active.Where(key => key.StartsWith(pattern[..^1], StringComparison.Ordinal)),
                _ => [pattern],
            })
            .Distinct(StringComparer.Ordinal),
    ];

    private static Result Validate(AccessManifest manifest)
    {
        if (!KeyPattern().IsMatch(manifest.ResourceServer ?? string.Empty) || manifest.ResourceServer!.Length > 50)
        {
            return AccessErrors.InvalidManifest("resourceServer must use lowercase letters, digits and dashes (max 50).");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 100)
        {
            return AccessErrors.InvalidManifest("name is required (max 100 characters).");
        }

        if (string.IsNullOrWhiteSpace(manifest.ClientId))
        {
            return AccessErrors.InvalidManifest("clientId is required.");
        }

        if (manifest.Permissions.Count > MaxPermissions || manifest.Roles.Count > MaxRoleTemplates)
        {
            return AccessErrors.InvalidManifest($"At most {MaxPermissions} permissions and {MaxRoleTemplates} roles.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ManifestPermission permission in manifest.Permissions)
        {
            if (!PermissionPattern().IsMatch(permission.Key ?? string.Empty) || !keys.Add(permission.Key!))
            {
                return AccessErrors.InvalidManifest($"Invalid or repeated permission '{permission.Key}': use 'resource:action'.");
            }

            if (permission.Requires is not (null or PermissionRequirements.None or PermissionRequirements.Mfa or PermissionRequirements.RecentAuthentication))
            {
                return AccessErrors.InvalidManifest($"Permission '{permission.Key}': requires must be none, mfa or recent_auth.");
            }
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (ManifestRole role in manifest.Roles)
        {
            if (!KeyPattern().IsMatch(role.Key ?? string.Empty) || !roles.Add(role.Key!) || string.IsNullOrWhiteSpace(role.Name))
            {
                return AccessErrors.InvalidManifest($"Invalid or repeated role '{role.Key}'.");
            }

            string? unknown = role.Permissions.FirstOrDefault(pattern =>
                pattern != "*" && !(pattern.EndsWith(":*", StringComparison.Ordinal) && keys.Any(key => key.StartsWith(pattern[..^1], StringComparison.Ordinal))) && !keys.Contains(pattern));
            if (unknown is not null)
            {
                return AccessErrors.InvalidManifest($"Role '{role.Key}' uses an unknown permission '{unknown}'.");
            }

            if ((role.GrantTo ?? []).Any(baseRole => baseRole is not (BaseRoles.Owner or BaseRoles.Admin or BaseRoles.Member)))
            {
                return AccessErrors.InvalidManifest($"Role '{role.Key}': grantTo only accepts owner, admin and member.");
            }
        }

        return Result.Success();
    }

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z][a-z0-9_-]*:[a-z][a-z0-9_-]*$")]
    private static partial Regex PermissionPattern();
}

/// <summary>An action on a resource type of the app: "invoice:approve".</summary>
public sealed class Permission
{
    private Permission()
    {
    }

    public Guid Id { get; private set; }

    public Guid ResourceServerId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Condition on the sign-in: none, mfa or recent_auth (step-up when it is not met).</summary>
    public string Requires { get; private set; } = PermissionRequirements.None;

    public DateTime? DeprecatedAtUtc { get; private set; }

    public bool IsActive => DeprecatedAtUtc is null;

    internal static Permission Create(Guid resourceServerId, string key, string? description, string requires) => new()
    {
        Id = Guid.CreateVersion7(),
        ResourceServerId = resourceServerId,
        Key = key,
        Description = description,
        Requires = requires,
    };

    internal void Update(string? description, string requires)
    {
        Description = description;
        Requires = requires;
        DeprecatedAtUtc = null;
    }

    internal void Deprecate(DateTime utcNow) => DeprecatedAtUtc ??= utcNow;
}

/// <summary>Role offered by the app to every organization. Read-only for organizations; granted by base role or by assignment.</summary>
public sealed class RoleTemplate
{
    private RoleTemplate()
    {
    }

    public Guid Id { get; private set; }

    public Guid ResourceServerId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Permission keys, wildcards already expanded.</summary>
    public string[] PermissionKeys { get; private set; } = [];

    /// <summary>Base roles that have this role implicitly.</summary>
    public string[] GrantTo { get; private set; } = [];

    internal static RoleTemplate Create(Guid resourceServerId, string key, string name, string? description, string[] permissions, string[] grantTo) => new()
    {
        Id = Guid.CreateVersion7(),
        ResourceServerId = resourceServerId,
        Key = key,
        Name = name.Trim(),
        Description = description,
        PermissionKeys = permissions,
        GrantTo = grantTo,
    };

    internal void Update(string name, string? description, string[] permissions, string[] grantTo)
    {
        Name = name.Trim();
        Description = description;
        PermissionKeys = permissions;
        GrantTo = grantTo;
    }
}

public static class PermissionRequirements
{
    public const string None = "none";
    public const string Mfa = "mfa";
    public const string RecentAuthentication = "recent_auth";
}

/// <summary>Organization roles of the Tenancy module (tenant_role claim).</summary>
public static class BaseRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
}
