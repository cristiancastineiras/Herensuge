namespace Herensuge.Modules.Oidc.Application.Authorization.Authorize;

/// <param name="RequestedTenantId">"tenant" parameter of the authorization request (organization switch).</param>
/// <param name="SelectedTenantId">Organization chosen earlier in this browser (SSO cookie).</param>
public sealed record AuthorizeQuery(
    Guid UserId,
    Guid SessionId,
    IReadOnlyList<string> AuthenticationMethods,
    Guid? RequestedTenantId,
    Guid? SelectedTenantId) : IQuery<AuthorizationDecision>;

public enum AuthorizationOutcome
{
    Granted,
    SessionInvalid,
    SelectTenant,
    Onboarding,
    TenantForbidden,
    MfaRequired,
    MfaSetupRequired,
}

public sealed record AuthorizationDecision(AuthorizationOutcome Outcome, AuthorizedUser? User = null);
