using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Herensuge.Common.Infrastructure.Web;

public static class WebHostingExtensions
{
    /// <summary>Traces, metrics and logs over OTLP (enabled when OTEL_EXPORTER_OTLP_ENDPOINT is set).</summary>
    public static IHostApplicationBuilder AddHerensugeTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        OpenTelemetryBuilder telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource("Herensuge.*"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime", "Herensuge.*"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Behind the TLS reverse proxy (Caddy) the app sees plain HTTP. When Herensuge:TrustForwardedHeaders
    /// is true every proxy is trusted, so the app must never be reachable without it.
    /// </summary>
    public static IServiceCollection AddHerensugeForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
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

        return services;
    }

    public static IServiceCollection AddHerensugeProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Strict defaults for every response, including the SPA served from wwwroot.</summary>
    public static IApplicationBuilder UseHerensugeSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers.ContentSecurityPolicy =
                    "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
                    "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
                return Task.CompletedTask;
            });
            await next();
        });

    public static WebApplication MapHerensugeHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return app;
    }

    internal static IServiceCollection AddHerensugeHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
            .AddCheck<ValkeyHealthCheck>("valkey", tags: ["ready"]);
        return services;
    }

    private sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
            }
        }
    }

    private sealed class ValkeyHealthCheck(IServiceProvider provider) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            IConnectionMultiplexer? multiplexer = provider.GetService<IConnectionMultiplexer>();
            if (multiplexer is null)
            {
                return HealthCheckResult.Healthy("In-memory cache (Development).");
            }

            try
            {
                await multiplexer.GetDatabase().PingAsync();
                return HealthCheckResult.Healthy();
            }
            catch (RedisException exception)
            {
                return HealthCheckResult.Unhealthy("Valkey is unreachable.", exception);
            }
        }
    }
}
