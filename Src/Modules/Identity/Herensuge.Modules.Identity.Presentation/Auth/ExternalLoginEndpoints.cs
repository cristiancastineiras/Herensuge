using System.Security.Claims;
using Herensuge.Modules.Identity.Application;
using Herensuge.Modules.Identity.Application.SignIn;
using Herensuge.Modules.Identity.Application.SignIn.SignInWithExternalLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace Herensuge.Modules.Identity.Presentation.Auth;

/// <summary>
/// Sign-in (or account linking) with Google and Microsoft. Browser navigations, not fetch calls:
/// the CSRF protection is OpenIddict's state token.
/// </summary>
internal sealed class ExternalLoginEndpoints : IEndpoint
{
    private const string ModeKey = ".herensuge.mode";
    private const string LinkMode = "link";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup(AuthRoutes.Prefix + "/external")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapGet("/{provider}", (
            string provider,
            string? returnUrl,
            string? mode,
            IOptions<ExternalProvidersOptions> options,
            IConfiguration configuration) =>
        {
            if (!options.Value.GetConfiguredProviders().Contains(provider) || ToProviderName(provider) is not { } providerName)
            {
                return Results.NotFound();
            }

            var properties = new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = providerName,
                [ModeKey] = mode == LinkMode ? LinkMode : "sign-in",
            })
            {
                RedirectUri = ReturnUrls.IsAllowed(returnUrl, configuration) ? returnUrl : "/",
            };

            return Results.Challenge(properties, [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
        });

        group.MapMethods("/{provider}/callback", [HttpMethods.Get, HttpMethods.Post], HandleCallbackAsync);
    }

    private static async Task<IResult> HandleCallbackAsync(
        string provider,
        HttpContext httpContext,
        ICommandHandler<SignInWithExternalLoginCommand, SignInResult> handler,
        CancellationToken cancellationToken)
    {
        AuthenticateResult result = await httpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        string returnUrl = result.Properties?.RedirectUri ?? "/";
        if (!result.Succeeded || result.Principal is not { } principal)
        {
            return Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl, "external_failed"));
        }

        string? subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
        {
            return Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl, "external_failed"));
        }

        // Microsoft "sub" is scoped to the directory: (tid, sub) is the stable identity.
        if (provider == ExternalProviders.Microsoft && principal.FindFirstValue("tid") is { Length: > 0 } tenant)
        {
            subject = $"{tenant}:{subject}";
        }

        string? email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
        if (email is null && provider == ExternalProviders.Microsoft && principal.FindFirstValue("preferred_username") is { } username && username.Contains('@', StringComparison.Ordinal))
        {
            email = username;
        }

        // Only Google vouches for the address; Microsoft emails are treated as unverified (nOAuth).
        bool emailVerified = provider == ExternalProviders.Google
            && string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        string? name = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);

        Guid? linkToUserId = null;
        if (result.Properties?.Items.TryGetValue(ModeKey, out string? mode) == true && mode == LinkMode)
        {
            ClaimsPrincipal? session = await IdentityProviderSession.AuthenticateAsync(httpContext);
            if (session is null)
            {
                return Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl, "link_requires_sign_in"));
            }

            linkToUserId = session.GetUserId();
        }

        Result<SignInResult> outcome = await handler.Handle(
            new SignInWithExternalLoginCommand(provider, subject, email, emailVerified, name, linkToUserId),
            cancellationToken);

        if (outcome.IsFailure)
        {
            return Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl, outcome.Error.Code));
        }

        switch (outcome.Value.Status)
        {
            case SignInStatus.MfaRequired:
                await SignInCookies.SignInMfaPendingAsync(httpContext, outcome.Value);
                return Results.Redirect(LoginPages.With(LoginPages.Mfa, returnUrl));

            case SignInStatus.SignedIn:
                await SignInCookies.SignInSessionAsync(httpContext, outcome.Value, tenantId: null);
                return Results.Redirect(returnUrl);

            default:
                return Results.Redirect(returnUrl);
        }
    }

    private static string? ToProviderName(string provider) => provider switch
    {
        ExternalProviders.Google => Providers.Google,
        ExternalProviders.Microsoft => Providers.Microsoft,
        _ => null,
    };
}
