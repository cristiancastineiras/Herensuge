namespace Herensuge.Modules.Access.Domain.ResourceServers;

/// <summary>
/// Catalog an app declares in its repository (herensuge.access.json) and syncs from its CI: its permissions
/// and the roles it offers to every organization. The API scope and audience derive from the key.
/// </summary>
/// <param name="EmbedPermissions">Permissions in the access token; null means true.</param>
public sealed record AccessManifest(
    string ResourceServer,
    string Name,
    string ClientId,
    bool? EmbedPermissions,
    IReadOnlyList<ManifestPermission> Permissions,
    IReadOnlyList<ManifestRole> Roles);

/// <param name="Key">"invoice:approve": the resource type and the action of AuthZEN requests.</param>
/// <param name="Requires">none, mfa or recent_auth.</param>
public sealed record ManifestPermission(string Key, string? Description, string? Requires);

/// <param name="Permissions">Keys, "invoice:*" or "*"; expanded when the manifest is applied.</param>
/// <param name="GrantTo">Organization base roles (owner, admin, member) that get this role without an assignment.</param>
public sealed record ManifestRole(
    string Key,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string>? GrantTo);
