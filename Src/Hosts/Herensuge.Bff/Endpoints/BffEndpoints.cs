using System.Security.Claims;
using System.Text.Json;
using Herensuge.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Bff.Endpoints;

public sealed record BffUser(
    string Subject,
    string? SessionId,
    string? Name,
    string? Email,
    string? TenantId,
    string? TenantRole,
    IReadOnlyList<string> AuthenticationMethods,
    long? AuthenticationTime,
    bool IsPlatformAdmin,
    string LogoutUrl);

/// <summary>/bff/*: sign-in, callback, current user and sign-out of the portal.</summary>
internal static class BffEndpoints
{
    private static readonly string[] CopiedClaims =
    [
        Claims.Subject, "sid", Claims.Name, Claims.Email, Claims.EmailVerified, "tenant_id", "tenant_role",
        Claims.AuthenticationMethodReference, Claims.AuthenticationTime, Claims.Role,
    ];

    private static readonly string[] InteractiveErrors =
        [Errors.LoginRequired, Errors.InteractionRequired, Errors.ConsentRequired, Errors.AccountSelectionRequired];

    public static WebApplication MapBffEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/bff").ExcludeFromDescription();

        // max_age (OIDC name): /bff/login?max_age=0 forces a fresh sign-in (sensitive operations).
        group.MapGet("/login", (string? returnUrl, string? tenant, string? prompt, [FromQuery(Name = "max_age")] int? maxAge) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = LocalOrRoot(returnUrl) };
            if (Guid.TryParse(tenant, out Guid tenantId))
            {
                properties.Parameters["tenant"] = tenantId.ToString();
            }

            if (prompt is PromptValues.None or PromptValues.Login)
            {
                properties.Parameters[Parameters.Prompt] = prompt;
            }

            if (maxAge is >= 0)
            {
                properties.Parameters[Parameters.MaxAge] = (long)maxAge.Value;
            }

            return Results.Challenge(properties, [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
        });

        group.MapMethods("/callback", [HttpMethods.Get, HttpMethods.Post], (Delegate)CallbackAsync);

        group.MapGet("/user", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuthenticateResult authenticated = await httpContext.AuthenticateAsync(BffDefaults.CookieScheme);
            if (!authenticated.Succeeded || authenticated.Principal is not { } user)
            {
                return Results.Unauthorized();
            }

            string? sessionId = user.FindFirstValue("sid");
            return Results.Ok(new BffUser(
                user.FindFirstValue(Claims.Subject) ?? string.Empty,
                sessionId,
                user.FindFirstValue(Claims.Name),
                user.FindFirstValue(Claims.Email),
                user.FindFirstValue("tenant_id"),
                user.FindFirstValue("tenant_role"),
                ReadArray(user, Claims.AuthenticationMethodReference),
                long.TryParse(user.FindFirstValue(Claims.AuthenticationTime), out long authTime) ? authTime : null,
                user.HasClaim(Claims.Role, "platform_admin"),
                $"/bff/logout?sid={Uri.EscapeDataString(sessionId ?? string.Empty)}"));
        });

        // Top-level navigation (it redirects to the IdP). The sid parameter, only readable by the SPA
        // through /bff/user, protects it against cross-site logout requests.
        group.MapGet("/logout", LogoutAsync);

        group.MapMethods("/signout-callback", [HttpMethods.Get, HttpMethods.Post], async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuthenticateResult result = await httpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            return Results.Redirect(LocalOrRoot(result.Properties?.RedirectUri));
        });

        return app;
    }

    private static async Task<IResult> CallbackAsync(HttpContext httpContext)
    {
        AuthenticateResult result = await httpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        string returnUrl = LocalOrRoot(result.Properties?.RedirectUri);

        if (!result.Succeeded || result.Principal is not { } principal)
        {
            string? error = result.Properties?.Items.TryGetValue(OpenIddictClientAspNetCoreConstants.Properties.Error, out string? value) == true ? value : null;
            return error is not null && InteractiveErrors.Contains(error)
                ? Results.Redirect($"/bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}")
                : Results.Redirect($"/?error={Uri.EscapeDataString(error ?? "sign_in_failed")}");
        }

        var identity = new ClaimsIdentity(BffDefaults.CookieScheme, Claims.Name, Claims.Role);
        foreach (Claim claim in principal.Claims.Where(claim => CopiedClaims.Contains(claim.Type)))
        {
            identity.AddClaim(new Claim(claim.Type, claim.Value, claim.ValueType));
        }

        AuthenticationProperties source = result.Properties!;
        var properties = new AuthenticationProperties { RedirectUri = returnUrl, IsPersistent = true };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = BffDefaults.AccessToken, Value = source.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken) ?? string.Empty },
            new AuthenticationToken { Name = BffDefaults.AccessTokenExpiresAt, Value = source.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessTokenExpirationDate) ?? string.Empty },
            new AuthenticationToken { Name = BffDefaults.RefreshToken, Value = source.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken) ?? string.Empty },
            new AuthenticationToken { Name = BffDefaults.IdentityToken, Value = source.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken) ?? string.Empty },
        ]);

        await httpContext.SignInAsync(BffDefaults.CookieScheme, new ClaimsPrincipal(identity), properties);
        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> LogoutAsync(
        string? sid,
        HttpContext httpContext,
        OpenIddictClientService client,
        ILogger<BffUser> logger)
    {
        AuthenticateResult authenticated = await httpContext.AuthenticateAsync(BffDefaults.CookieScheme);
        if (!authenticated.Succeeded || authenticated.Principal is not { } user || authenticated.Properties is not { } properties)
        {
            return Results.Redirect("/");
        }

        if (!string.Equals(sid, user.FindFirstValue("sid"), StringComparison.Ordinal))
        {
            return Results.Problem(
                title: "Invalid logout request.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "Bff.InvalidLogoutRequest" });
        }

        string? refreshToken = properties.GetTokenValue(BffDefaults.RefreshToken);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await client.RevokeTokenAsync(new OpenIddictClientModels.RevocationRequest
                {
                    Token = refreshToken,
                    TokenTypeHint = TokenTypeHints.RefreshToken,
                    CancellationToken = httpContext.RequestAborted,
                });
            }
            catch (Exception exception) when (exception is OpenIddictExceptions.ProtocolException or HttpRequestException)
            {
                logger.LogWarning(exception, "The refresh token could not be revoked; the end session request revokes it anyway");
            }
        }

        string? idToken = properties.GetTokenValue(BffDefaults.IdentityToken);
        await httpContext.SignOutAsync(BffDefaults.CookieScheme);

        var signOut = new AuthenticationProperties { RedirectUri = "/" };
        if (!string.IsNullOrEmpty(idToken))
        {
            signOut.Items[OpenIddictClientAspNetCoreConstants.Properties.IdentityTokenHint] = idToken;
        }

        return Results.SignOut(signOut, [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static string LocalOrRoot(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : "/";

    private static List<string> ReadArray(ClaimsPrincipal user, string type)
    {
        var values = new List<string>();
        foreach (Claim claim in user.FindAll(type))
        {
            if (claim.Value.StartsWith('['))
            {
                values.AddRange(JsonSerializer.Deserialize<string[]>(claim.Value) ?? []);
            }
            else
            {
                values.Add(claim.Value);
            }
        }

        return values;
    }
}
