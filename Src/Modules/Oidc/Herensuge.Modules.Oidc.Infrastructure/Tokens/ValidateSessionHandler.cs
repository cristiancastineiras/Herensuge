using System.Security.Claims;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Contracts;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace Herensuge.Modules.Oidc.Infrastructure.Tokens;

/// <summary>
/// Every API call checks the SSO session behind the access token: a closed session loses access at once,
/// without waiting for the outbox to revoke its tokens. A user token without session is rejected.
/// </summary>
internal sealed class ValidateSessionHandler(IIdentityModuleApi identity) : IOpenIddictValidationHandler<ProcessAuthenticationContext>
{
    /// <summary>client_credentials token of an application's backend: no user, so no session (sub = client_id, no sid).</summary>
    public static bool IsMachineToken(ClaimsPrincipal principal) =>
        principal.GetClaim(HerensugeClaimTypes.SessionId) is null
        && principal.GetClaim(Claims.Subject) is { Length: > 0 } subject
        && subject == principal.GetClaim(Claims.ClientId);

    public async ValueTask HandleAsync(ProcessAuthenticationContext context)
    {
        if (context.AccessTokenPrincipal is not { } principal || IsMachineToken(principal))
        {
            return;
        }

        if (!Guid.TryParse(principal.GetClaim(Claims.Subject), out Guid userId)
            || !Guid.TryParse(principal.GetClaim(HerensugeClaimTypes.SessionId), out Guid sessionId)
            || !await identity.IsSessionActiveAsync(sessionId, userId, context.CancellationToken))
        {
            context.Reject(error: Errors.InvalidToken, description: "The session has ended.");
        }
    }
}
