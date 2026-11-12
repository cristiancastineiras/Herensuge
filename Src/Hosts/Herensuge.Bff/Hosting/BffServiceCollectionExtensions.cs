using System.Security.Cryptography.X509Certificates;
using Herensuge.Bff.Sessions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using OpenIddict.Client;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

namespace Herensuge.Bff.Hosting;

internal static class BffServiceCollectionExtensions
{
    public static IServiceCollection AddBffAuthentication(this IServiceCollection services, BffOptions bff)
    {
        if (string.IsNullOrWhiteSpace(bff.ClientSecret))
        {
            throw new InvalidOperationException("Bff:ClientSecret is not configured.");
        }

        services.AddAuthentication(BffDefaults.CookieScheme)
            .AddCookie(BffDefaults.CookieScheme, options =>
            {
                options.Cookie.Name = BffDefaults.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = bff.SessionLifetime;
                options.SlidingExpiration = true;

                // The SPA decides what to do with a 401; the BFF never redirects API calls.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddSingleton<ITicketStore, DistributedTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(BffDefaults.CookieScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);

        services.AddOpenIddict().AddClient(options =>
        {
            options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();

            // State tokens are protected with Data Protection (shared key ring), not with these keys.
            options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            options.UseDataProtection();

            // The BFF has no database (no OpenIddict core stores): state tokens are self-contained.
            options.DisableTokenStorage();

            options.UseAspNetCore()
                .EnableRedirectionEndpointPassthrough()
                .EnablePostLogoutRedirectionEndpointPassthrough()
                .EnableErrorPassthrough();
            options.UseSystemNetHttp().SetProductInformation(typeof(BffOptions).Assembly);

            options.SetRedirectionEndpointUris("bff/callback")
                .SetPostLogoutRedirectionEndpointUris("bff/signout-callback");

            var registration = new OpenIddictClientRegistration
            {
                Issuer = new Uri(bff.Authority, UriKind.Absolute),
                ClientId = bff.ClientId,
                ClientSecret = bff.ClientSecret,
                RedirectUri = new Uri("bff/callback", UriKind.Relative),
                PostLogoutRedirectUri = new Uri("bff/signout-callback", UriKind.Relative),
            };
            registration.Scopes.UnionWith(bff.Scopes);
            options.AddRegistration(registration);
        });

        if (!string.IsNullOrWhiteSpace(bff.BackchannelAddress))
        {
            var publicAuthority = new Uri(bff.Authority, UriKind.Absolute);
            var internalAddress = new Uri(bff.BackchannelAddress, UriKind.Absolute);
            services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(
                builder => builder.AdditionalHandlers.Add(new BackchannelRewriteHandler(publicAuthority, internalAddress))));
        }

        services.AddScoped<AccessTokenProvider>();
        return services;
    }

    /// <summary>
    /// Valkey for sessions, locks and the Data Protection key ring. In-memory only in Development. The key ring is
    /// always encrypted at rest: with DataProtection:CertificatePath when it is set, otherwise with
    /// DataProtection:KeyRingKey (required).
    /// </summary>
    public static IServiceCollection AddBffState(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        IDataProtectionBuilder dataProtection = services.AddDataProtection().SetApplicationName("herensuge-bff");
        string? certificatePath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            dataProtection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                configuration["DataProtection:CertificatePassword"],
                X509KeyStorageFlags.EphemeralKeySet));
        }
        else
        {
            var keyRingKey = KeyRingKey.From(configuration["DataProtection:KeyRingKey"]);
            services.AddSingleton(keyRingKey);
            services.AddOptions<KeyManagementOptions>().Configure(options => options.XmlEncryptor = new KeyRingXmlEncryptor(keyRingKey));
        }

        string? valkey = configuration.GetConnectionString("Valkey");
        if (string.IsNullOrWhiteSpace(valkey))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("ConnectionStrings:Valkey is required outside Development.");
            }

            services.AddDistributedMemoryCache();
            services.AddSingleton<ISessionLock, InMemorySessionLock>();
            services.AddHealthChecks();
            return services;
        }

        var configurationOptions = ConfigurationOptions.Parse(valkey);
        configurationOptions.AbortOnConnectFail = false;
        IConnectionMultiplexer multiplexer = ConnectionMultiplexer.Connect(configurationOptions);

        services.AddSingleton(multiplexer);
        services.AddStackExchangeRedisCache(options =>
        {
            options.InstanceName = "herensuge-bff:";
            options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer);
        });
        services.AddSingleton<ISessionLock, ValkeySessionLock>();
        dataProtection.PersistKeysToStackExchangeRedis(multiplexer, "herensuge-bff:data-protection-keys");

        services.AddHealthChecks().AddCheck("valkey", () =>
        {
            try
            {
                multiplexer.GetDatabase().Ping();
                return HealthCheckResult.Healthy();
            }
            catch (RedisException exception)
            {
                return HealthCheckResult.Unhealthy("Valkey is unreachable.", exception);
            }
        }, tags: ["ready"]);

        return services;
    }

    /// <summary>/api/* → Herensuge.Api with the session's access token; the BFF cookie is never forwarded.</summary>
    public static IServiceCollection AddBffProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(context =>
            {
                context.AddRequestHeaderRemove("Cookie");
                context.AddRequestHeaderRemove(BffDefaults.CsrfHeader);
                context.AddRequestTransform(transform =>
                {
                    if (transform.HttpContext.Items[BffDefaults.AccessToken] is string token)
                    {
                        transform.ProxyRequest.Headers.Authorization = new("Bearer", token);
                    }

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    public static IServiceCollection AddBffHosting(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            if (configuration.GetValue<bool>("Herensuge:TrustForwardedHeaders"))
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });

        services.AddProblemDetails();

        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration["Spa:DevServerUrl"]))
        {
            services.AddHttpForwarder();
        }

        return services;
    }

    public static IHostApplicationBuilder AddBffTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        OpenTelemetryBuilder telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("herensuge-bff"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
