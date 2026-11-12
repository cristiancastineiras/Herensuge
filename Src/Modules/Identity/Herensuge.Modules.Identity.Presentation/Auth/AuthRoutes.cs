using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using Herensuge.Common.Application.Context;
using Herensuge.Modules.Identity.Application.SignIn;
using Herensuge.Modules.Identity.Application.SignIn.SignOut;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Modules.Identity.Presentation.Auth;

/// <summary>
/// /api/v1/auth: JSON API used by the login SPA (same origin as the IdP). Cookie-authenticated, so
/// every state-changing call needs the X-CSRF header.
/// </summary>
internal static class AuthRoutes
{
    public const string Prefix = "/api/v1/auth";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder app) =>
        app.MapGroup(Prefix)
            .WithTags("Auth")
            .RequireCsrfHeader()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AddEndpointFilter(EnsureDeviceCookieAsync);

    /// <summary>Long-lived random id that lets us recognise the browser and warn about new devices.</summary>
    private static ValueTask<object?> EnsureDeviceCookieAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        if (string.IsNullOrEmpty(httpContext.Request.Cookies[HerensugeCookies.Device]))
        {
            string deviceId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            httpContext.Items[RequestContextKeys.DeviceId] = deviceId;
            httpContext.Response.Cookies.Append(HerensugeCookies.Device, deviceId, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(400),
            });
        }

        return next(context);
    }
}

public sealed record SignInResponse(string Status, IReadOnlyList<string> MfaMethods);

/// <summary>Turns a sign-in outcome into cookies: the SSO cookie, or the 5-minute "MFA pending" cookie.</summary>
internal static class SignInCookies
{
    public static async Task<IResult> CompleteAsync(HttpContext httpContext, SignInResult result, Guid? tenantId = null)
    {
        switch (result.Status)
        {
            case SignInStatus.SignedIn:
                await SignInSessionAsync(httpContext, result, tenantId);
                return Results.Ok(new SignInResponse("signed-in", []));

            case SignInStatus.MfaRequired:
                await SignInMfaPendingAsync(httpContext, result);
                return Results.Ok(new SignInResponse("mfa-required", result.MfaMethods));

            default:
                return Results.Ok(new SignInResponse("linked", []));
        }
    }

    /// <summary>
    /// Issues the SSO cookie. A different session already open in this browser is revoked, so a
    /// re-authentication does not leave an orphan session behind.
    /// </summary>
    public static async Task SignInSessionAsync(HttpContext httpContext, SignInResult result, Guid? tenantId)
    {
        Guid sessionId = result.SessionId!.Value;
        ClaimsPrincipal? previous = await IdentityProviderSession.AuthenticateAsync(httpContext);
        if (previous?.GetSessionId() is { } previousSessionId && previousSessionId != sessionId)
        {
            await httpContext.RequestServices
                .GetRequiredService<ICommandHandler<SignOutCommand>>()
                .Handle(new SignOutCommand(previousSessionId), httpContext.RequestAborted);
        }

        await IdentityProviderSession.SignInAsync(
            httpContext,
            result.UserId,
            sessionId,
            result.Methods,
            result.AuthenticatedAtUtc!.Value,
            tenantId);
        await httpContext.SignOutAsync(HerensugeAuthenticationSchemes.MfaPending);
    }

    public static Task SignInMfaPendingAsync(HttpContext httpContext, SignInResult result)
    {
        var identity = new ClaimsIdentity(HerensugeAuthenticationSchemes.MfaPending, HerensugeClaimTypes.Name, HerensugeClaimTypes.Role);
        identity.AddClaim(new Claim(HerensugeClaimTypes.Subject, result.UserId.ToString()));
        foreach (string method in result.Methods)
        {
            identity.AddClaim(new Claim(HerensugeClaimTypes.AuthenticationMethod, method));
        }

        return httpContext.SignInAsync(
            HerensugeAuthenticationSchemes.MfaPending,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }
}

/// <summary>Who is answering a second factor: a pending sign-in, or a signed-in user stepping up.</summary>
internal sealed record MfaContext(Guid UserId, IReadOnlyList<string> FirstFactorMethods, Guid? StepUpSessionId, Guid? TenantId)
{
    public static async Task<MfaContext?> ResolveAsync(HttpContext httpContext)
    {
        AuthenticateResult pending = await httpContext.AuthenticateAsync(HerensugeAuthenticationSchemes.MfaPending);
        if (pending.Succeeded && pending.Principal is not null)
        {
            return new MfaContext(pending.Principal.GetUserId(), pending.Principal.GetAuthenticationMethods(), null, null);
        }

        ClaimsPrincipal? session = await IdentityProviderSession.AuthenticateAsync(httpContext);
        return session is null
            ? null
            : new MfaContext(session.GetUserId(), session.GetAuthenticationMethods(), session.GetSessionId(), session.GetTenantId());
    }
}

internal static class AuthProblems
{
    public static IResult NotSignedIn() =>
        ApiResults.Problem(Error.Unauthorized("Auth.NotSignedIn", "There is no sign-in in progress."));
}
