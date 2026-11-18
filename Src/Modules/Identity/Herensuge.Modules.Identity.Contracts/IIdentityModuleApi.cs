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

    /// <summary>
    /// Same check without sliding the expiration (read-only): it runs on every request, so a closed
    /// session loses access at once.
    /// </summary>
    Task<bool> IsSessionActiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Ends the SSO session (OIDC logout). Its tokens are revoked through SessionRevoked.</summary>
    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

/// <param name="ProfileFields">Registration data asked by the applications ("company" → "Acme"), all of them.</param>
public sealed record UserSnapshot(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    bool IsPlatformAdmin,
    bool HasMfa,
    IReadOnlyDictionary<string, string> ProfileFields);

public sealed record UserSummary(Guid Id, string Email, string DisplayName);
