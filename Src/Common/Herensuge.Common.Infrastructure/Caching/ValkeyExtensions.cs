using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Herensuge.Common.Infrastructure.Caching;

public static class ValkeyExtensions
{
    /// <summary>
    /// Valkey backs IDistributedCache (WebAuthn challenges, short-lived state). Without a connection
    /// string the app falls back to an in-memory cache, which is only allowed in Development.
    /// </summary>
    internal static IServiceCollection AddValkey(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        string? connectionString = configuration.GetConnectionString("Valkey");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("ConnectionStrings:Valkey is required outside Development.");
            }

            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddStackExchangeRedisCache(options => options.InstanceName = "herensuge:");
        services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((options, provider) =>
                options.ConnectionMultiplexerFactory = () => Task.FromResult(provider.GetRequiredService<IConnectionMultiplexer>()));

        return services;
    }
}
