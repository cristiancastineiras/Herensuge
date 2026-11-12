using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Infrastructure.Clients;
using Herensuge.Modules.Oidc.Infrastructure.Database;
using Herensuge.Modules.Oidc.Infrastructure.Jobs;
using Herensuge.Modules.Oidc.Infrastructure.Keys;
using Herensuge.Modules.Oidc.Infrastructure.Seeding;
using Herensuge.Modules.Oidc.Infrastructure.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Infrastructure;

public static class OidcModule
{
    public const string Schema = "oidc";

    public static IServiceCollection AddOidcModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        OidcOptions options = configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();
        services.AddOptions<OidcOptions>().Bind(configuration.GetSection(OidcOptions.SectionName));

        string keyDirectory = Path.IsPathRooted(options.Keys.Directory)
            ? options.Keys.Directory
            : Path.Combine(environment.ContentRootPath, options.Keys.Directory);
        X509Certificate2 signing = OidcCertificates.LoadOrCreate(
            Path.Combine(keyDirectory, "signing.pfx"), options.Keys.Password, "Herensuge OIDC signing", X509KeyUsageFlags.DigitalSignature, options.Keys.AutoGenerate);
        X509Certificate2 encryption = OidcCertificates.LoadOrCreate(
            Path.Combine(keyDirectory, "encryption.pfx"), options.Keys.Password, "Herensuge OIDC encryption", X509KeyUsageFlags.KeyEncipherment, options.Keys.AutoGenerate);

        services.AddModuleDbContext<OidcDbContext>(Schema);

        services.AddOpenIddict()
            .AddCore(core => core
                .UseEntityFrameworkCore()
                .UseDbContext<OidcDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(server =>
            {
                server.SetIssuer(new Uri(options.Issuer, UriKind.Absolute));
                server.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo")
                    .SetRevocationEndpointUris("connect/revocation")
                    .SetIntrospectionEndpointUris("connect/introspection")
                    .SetEndSessionEndpointUris("connect/logout");

                server.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
                server.RequireProofKeyForCodeExchange();
                server.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, HerensugeScopes.Api);
                server.RegisterClaims(
                    Claims.Subject, Claims.Name, Claims.Email, Claims.EmailVerified, Claims.Role,
                    Claims.AuthenticationMethodReference, Claims.AuthenticationTime,
                    HerensugeClaimTypes.SessionId, HerensugeClaimTypes.TenantId, HerensugeClaimTypes.TenantRole);

                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(5))
                    .SetIdentityTokenLifetime(TimeSpan.FromMinutes(5))
                    .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(7))
                    .SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(15));

                server.AddSigningCertificate(signing).AddEncryptionCertificate(encryption);

                // Access tokens are signed JWTs so the SaaS APIs can validate them with the JWKS.
                server.DisableAccessTokenEncryption();

                server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();
                validation.AddAudiences(HerensugeScopes.ApiResource);

                // Each API call checks the authorization is still valid: a revoked session cuts access at once.
                validation.EnableAuthorizationEntryValidation();
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(HerensugePolicies.Api, policy => policy
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasApiScope(context.User)))
            .AddPolicy(HerensugePolicies.PlatformAdmin, policy => policy
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    HasApiScope(context.User)
                    && context.User.HasClaim(HerensugeClaimTypes.Role, PlatformRoles.PlatformAdmin)
                    && HasAuthenticationMethod(context.User, AuthenticationMethods.Passkey)));

        services.AddScoped<IClientRegistry, OpenIddictClientRegistry>();
        services.AddScoped<ITokenRevoker, TokenRevoker>();
        services.AddHostedService<OidcSeeder>();
        services.AddHostedService<OidcPruningJob>();

        services.AddModuleMessaging(new MessagingModule("oidc", Schema, hasOutbox: false, AssemblyReference.Assembly));
        return services;
    }

    private static bool HasApiScope(ClaimsPrincipal user) => OpenIddict.Abstractions.OpenIddictExtensions.HasScope(user, HerensugeScopes.Api);

    private static bool HasAuthenticationMethod(ClaimsPrincipal user, string method) =>
        user.FindAll(HerensugeClaimTypes.AuthenticationMethod).Any(claim =>
            claim.Value == method
            || (claim.Value.StartsWith('[') && (JsonSerializer.Deserialize<string[]>(claim.Value) ?? []).Contains(method)));
}
