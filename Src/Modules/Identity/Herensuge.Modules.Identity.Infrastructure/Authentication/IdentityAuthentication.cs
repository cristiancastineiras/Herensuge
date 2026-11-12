using System.Globalization;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Application;
using Herensuge.Modules.Identity.Application.Sessions.ValidateSession;
using Herensuge.Modules.Identity.Domain.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Identity.Infrastructure.Authentication;

internal static class IdentityAuthentication
{
    public const string GoogleCallbackPath = "api/v1/auth/external/google/callback";
    public const string MicrosoftCallbackPath = "api/v1/auth/external/microsoft/callback";

    private const string ValidatedAtKey = ".herensuge.validated_at";
    private static readonly TimeSpan ValidationInterval = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddCookie(HerensugeAuthenticationSchemes.IdentityProvider, options =>
            {
                options.Cookie.Name = HerensugeCookies.IdentityProvider;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                // Lax: the cookie must travel on the top-level redirect from client apps to /connect/authorize.
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = UserSession.IdleTimeout;
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = StatusCode(StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = StatusCode(StatusCodes.Status403Forbidden);
                options.Events.OnValidatePrincipal = ValidateSessionAsync;
            })
            .AddCookie(HerensugeAuthenticationSchemes.MfaPending, options =>
            {
                options.Cookie.Name = HerensugeCookies.MfaPending;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = StatusCode(StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = StatusCode(StatusCodes.Status403Forbidden);
            });

        AddExternalProviders(services, configuration);
        return services;
    }

    /// <summary>Google and Microsoft through OpenIddict.Client. Unconfigured providers are not registered.</summary>
    private static void AddExternalProviders(IServiceCollection services, IConfiguration configuration)
    {
        ExternalProvidersOptions providers = configuration.GetSection(ExternalProvidersOptions.SectionName).Get<ExternalProvidersOptions>() ?? new();

        services.AddOpenIddict().AddClient(options =>
        {
            options.AllowAuthorizationCodeFlow();

            // State tokens are protected with ASP.NET Data Protection (key ring in PostgreSQL), so the
            // ephemeral keys below are never used to protect anything that must survive a restart.
            options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            options.UseDataProtection();

            options.UseAspNetCore().EnableRedirectionEndpointPassthrough();
            options.UseSystemNetHttp().SetProductInformation(typeof(IdentityAuthentication).Assembly);
            options.SetRedirectionEndpointUris(GoogleCallbackPath, MicrosoftCallbackPath);

            OpenIddictClientWebIntegrationBuilder web = options.UseWebProviders();
            if (providers.Google.IsConfigured)
            {
                web.AddGoogle(google => google
                    .SetClientId(providers.Google.ClientId!)
                    .SetClientSecret(providers.Google.ClientSecret!)
                    .SetRedirectUri(GoogleCallbackPath)
                    .AddScopes(Scopes.Email, Scopes.Profile));
            }

            if (providers.Microsoft.IsConfigured)
            {
                web.AddMicrosoft(microsoft => microsoft
                    .SetClientId(providers.Microsoft.ClientId!)
                    .SetClientSecret(providers.Microsoft.ClientSecret!)
                    .SetTenant(string.IsNullOrWhiteSpace(providers.Microsoft.Tenant) ? "common" : providers.Microsoft.Tenant)
                    .SetRedirectUri(MicrosoftCallbackPath)
                    .AddScopes(Scopes.Email, Scopes.Profile));
            }
        });
    }

    /// <summary>Every minute the cookie is checked against identity.user_sessions (revoked or expired → signed out).</summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        DateTimeOffset now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (context.Properties.Items.TryGetValue(ValidatedAtKey, out string? validatedAt)
            && DateTimeOffset.TryParse(validatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset last)
            && now - last < ValidationInterval)
        {
            return;
        }

        bool hasIds = Guid.TryParse(context.Principal?.FindFirst(HerensugeClaimTypes.Subject)?.Value, out Guid userId)
            & Guid.TryParse(context.Principal?.FindFirst(HerensugeClaimTypes.SessionId)?.Value, out Guid sessionId);

        Result result = hasIds
            ? await context.HttpContext.RequestServices
                .GetRequiredService<ICommandHandler<ValidateSessionCommand>>()
                .Handle(new ValidateSessionCommand(sessionId, userId), context.HttpContext.RequestAborted)
            : SessionErrors.Inactive;

        if (result.IsFailure)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        context.Properties.Items[ValidatedAtKey] = now.ToString("O", CultureInfo.InvariantCulture);
        context.ShouldRenew = true;
    }

    private static Func<RedirectContext<CookieAuthenticationOptions>, Task> StatusCode(int statusCode) => context =>
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    };
}
