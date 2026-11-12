namespace Herensuge.Modules.Identity.Contracts;

/// <summary>Synchronous, in-process queries other modules may run against Identity.</summary>
public interface IIdentityModuleApi
{
    Task<UserSnapshot?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummary>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <param name="email">Normalized (trimmed, lower case) address.</param>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>True when the session exists, belongs to the user and is active. Slides its expiration.</summary>
    Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Ends the SSO session (OIDC logout). Its tokens are revoked through SessionRevoked.</summary>
    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

public sealed record UserSnapshot(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    bool IsPlatformAdmin,
    bool HasMfa);

public sealed record UserSummary(Guid Id, string Email, string DisplayName);
