namespace Herensuge.Modules.Oidc.Application.Authorization.Authorize;

/// <param name="ClientId">Client of the authorization request: its login page says which methods and data it needs.</param>
/// <param name="RequestedTenantId">"tenant" parameter of the authorization request (organization switch).</param>
/// <param name="SelectedTenantId">Organization chosen earlier in this browser (SSO cookie).</param>
public sealed record AuthorizeQuery(
    string ClientId,
    Guid UserId,
    Guid SessionId,
    IReadOnlyList<string> AuthenticationMethods,
    Guid? RequestedTenantId,
    Guid? SelectedTenantId) : IQuery<AuthorizationDecision>;

public enum AuthorizationOutcome
{
    Granted,
    SessionInvalid,

    /// <summary>The session was started with a method the client does not accept: sign in again with another one.</summary>
    SignInMethodNotAllowed,

    /// <summary>The client asks for registration data the user has not given yet.</summary>
    ProfileIncomplete,
    SelectTenant,
    Onboarding,
    TenantForbidden,
    MfaRequired,
    MfaSetupRequired,
}

/// <param name="ProfileFields">The client's registration data of the user (profile_fields claim).</param>
public sealed record AuthorizationDecision(
    AuthorizationOutcome Outcome,
    AuthorizedUser? User = null,
    IReadOnlyDictionary<string, string>? ProfileFields = null);
