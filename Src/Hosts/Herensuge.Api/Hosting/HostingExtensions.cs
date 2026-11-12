using System.Net;
using System.Threading.RateLimiting;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Presentation.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Api.Hosting;

internal static class HostingExtensions
{
    /// <summary>Applies the migrations of every module schema (identity, tenancy, oidc, audit, notifications, common).</summary>
    public static async Task MigrateDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Herensuge.Migrations");
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        foreach (ModuleDatabase database in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(database.DbContextType);
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Schema {Schema} is up to date", database.Schema);
        }
    }

    /// <summary>
    /// Development/testing: migrates before any other hosted service starts (they start in registration
    /// order), so seeders and outbox jobs never see a missing schema. Production uses --migrate instead.
    /// </summary>
    public static IServiceCollection AddStartupMigrations(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            services.AddHostedService<StartupMigrator>();
        }

        return services;
    }

    /// <summary>Migrations, seeders and outbox/inbox jobs (they need the database). Used while generating the OpenAPI document.</summary>
    public static IServiceCollection RemoveHerensugeHostedServices(this IServiceCollection services)
    {
        ServiceDescriptor[] jobs = [.. services.Where(service =>
            service.ServiceType == typeof(IHostedService)
            && service.ImplementationType?.Namespace?.StartsWith("Herensuge", StringComparison.Ordinal) == true)];
        foreach (ServiceDescriptor job in jobs)
        {
            services.Remove(job);
        }

        return services;
    }

    private sealed class StartupMigrator(IServiceProvider services) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => services.MigrateDatabasesAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Per-IP limits on the sign-in API (in memory, per instance).</summary>
    public static IServiceCollection AddHerensugeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        int strictLimit = configuration.GetValue("RateLimiting:StrictPermitLimit", 10);
        int limit = configuration.GetValue("RateLimiting:PermitLimit", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Try again in a minute.",
                        Extensions = { ["code"] = "General.TooManyRequests" },
                    },
                });
            };

            // shortcut: limits live in each instance's memory; with several instances move them to Valkey.
            options.AddPolicy(RateLimitPolicies.AuthStrict, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = strictLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }

    private static string ClientAddress(HttpContext context)
    {
        IPAddress? address = context.Connection.RemoteIpAddress;
        return address is null ? "unknown" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}
