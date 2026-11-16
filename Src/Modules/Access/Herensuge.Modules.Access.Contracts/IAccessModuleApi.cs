namespace Herensuge.Modules.Access.Contracts;

/// <summary>Synchronous, in-process queries other modules may run against Access.</summary>
public interface IAccessModuleApi
{
    /// <summary>
    /// Roles and permissions of the user in the organization for the application behind the token audiences.
    /// Null when the token is not for exactly one Access application (one token, one API) or the user is not a member.
    /// </summary>
    Task<AccessTokenClaims?> GetTokenClaimsAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> audiences, CancellationToken cancellationToken);
}

/// <param name="Permissions">Null when the application keeps them out of the token (large catalogs).</param>
public sealed record AccessTokenClaims(IReadOnlyList<string> Roles, IReadOnlyList<string>? Permissions);
