namespace Herensuge.Modules.Oidc.Application.Authorization.ValidateTokenRequest;

/// <summary>
/// Re-checked on every code redemption and refresh: the session, the user and the membership must
/// still be valid. Returns fresh values for the claims that may have changed (name, role...).
/// </summary>
public sealed record ValidateTokenRequestQuery(
    Guid UserId,
    Guid SessionId,
    Guid TenantId,
    IReadOnlyList<string> AuthenticationMethods) : IQuery<AuthorizedUser>;
