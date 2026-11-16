namespace Herensuge.Modules.Access.Contracts;

/// <summary>Synchronous, in-process queries other modules may run against Access.</summary>
public interface IAccessModuleApi
{
    /// <summary>
    /// Roles and permissions of the user in the organization for the application behind the token audiences
    /// (access token and userinfo). Null when the audiences are not exactly one Access application (one token, one
    /// API) or the user is not a member of an active organization.
    /// </summary>
    Task<AccessClaims?> GetAccessClaimsAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> audiences, CancellationToken cancellationToken);

    /// <summary>Decides one permission of an application against the database (Herensuge's own checks; the session is already validated).</summary>
    Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string resourceServer, string permission, CancellationToken cancellationToken);
}

/// <param name="Permissions">Granted without condition.</param>
/// <param name="ConditionalPermissions">Granted, but only with a condition (MFA, recent sign-in…): the API asks the PDP.</param>
/// <param name="EmbedPermissions">Whether the permissions go in the access token (userinfo always carries them).</param>
public sealed record AccessClaims(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> ConditionalPermissions,
    bool EmbedPermissions);
