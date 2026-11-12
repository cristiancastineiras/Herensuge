using System.Globalization;
using System.Security.Claims;
using Herensuge.Common.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Herensuge.Common.Presentation.Security;

/// <summary>
/// The SSO cookie of Herensuge (__Host-herensuge-idp). It only carries identifiers: the session is
/// validated against identity.user_sessions every minute.
/// </summary>
public static class IdentityProviderSession
{
    public static async Task<ClaimsPrincipal?> AuthenticateAsync(HttpContext httpContext)
    {
        AuthenticateResult result = await httpContext.AuthenticateAsync(HerensugeAuthenticationSchemes.IdentityProvider);
        return result.Succeeded ? result.Principal : null;
    }

    public static Task SignInAsync(
        HttpContext httpContext,
        Guid userId,
        Guid sessionId,
        IEnumerable<string> authenticationMethods,
        DateTime authenticatedAtUtc,
        Guid? tenantId)
    {
        var identity = new ClaimsIdentity(
            HerensugeAuthenticationSchemes.IdentityProvider,
            HerensugeClaimTypes.Name,
            HerensugeClaimTypes.Role);

        identity.AddClaim(new Claim(HerensugeClaimTypes.Subject, userId.ToString()));
        identity.AddClaim(new Claim(HerensugeClaimTypes.SessionId, sessionId.ToString()));
        foreach (string method in authenticationMethods.Distinct(StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(HerensugeClaimTypes.AuthenticationMethod, method));
        }

        long authTime = new DateTimeOffset(DateTime.SpecifyKind(authenticatedAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        identity.AddClaim(new Claim(
            HerensugeClaimTypes.AuthenticationTime,
            authTime.ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));

        if (tenantId is { } tenant)
        {
            identity.AddClaim(new Claim(HerensugeClaimTypes.TenantId, tenant.ToString()));
        }

        return httpContext.SignInAsync(
            HerensugeAuthenticationSchemes.IdentityProvider,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    /// <summary>Re-issues the cookie with another active organization.</summary>
    public static Task SetActiveTenantAsync(HttpContext httpContext, ClaimsPrincipal principal, Guid tenantId) =>
        SignInAsync(
            httpContext,
            principal.GetUserId(),
            principal.GetSessionId() ?? throw new InvalidOperationException("The session cookie has no sid."),
            principal.GetAuthenticationMethods(),
            principal.GetAuthenticationTime()?.UtcDateTime ?? DateTime.UtcNow,
            tenantId);

    public static async Task SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(HerensugeAuthenticationSchemes.IdentityProvider);
        await httpContext.SignOutAsync(HerensugeAuthenticationSchemes.MfaPending);
    }
}
