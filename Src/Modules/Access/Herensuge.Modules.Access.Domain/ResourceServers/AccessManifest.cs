namespace Herensuge.Modules.Access.Domain.ResourceServers;

/// <summary>
/// Catalog an app declares in its repository (herensuge.access.json) and syncs from its CI: its permissions, the roles
/// it offers to every organization and the features plans can include. The API scope and audience derive from the key.
/// </summary>
/// <param name="EmbedPermissions">Permissions in the access token; null means true.</param>
/// <param name="Features">Contractable features (plan D5); null means none.</param>
public sealed record AccessManifest(
    string ResourceServer,
    string Name,
    string ClientId,
    bool? EmbedPermissions,
    IReadOnlyList<ManifestPermission> Permissions,
    IReadOnlyList<ManifestRole> Roles,
    IReadOnlyList<ManifestFeature>? Features = null);

/// <param name="Key">"invoice:approve": the resource type and the action of AuthZEN requests.</param>
/// <param name="Requires">none, mfa, recent_auth or trusted_network.</param>
/// <param name="Feature">Key of a feature of the manifest: without it in the organization's plan, the permission grants nothing.</param>
public sealed record ManifestPermission(string Key, string? Description, string? Requires, string? Feature = null);

/// <param name="Permissions">Keys, "invoice:*" or "*"; expanded when the manifest is applied.</param>
/// <param name="GrantTo">Organization base roles (owner, admin, member) that get this role without an assignment.</param>
public sealed record ManifestRole(
    string Key,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string>? GrantTo);

/// <param name="Key">"approvals": lowercase letters, digits and dashes.</param>
public sealed record ManifestFeature(string Key, string Name);
