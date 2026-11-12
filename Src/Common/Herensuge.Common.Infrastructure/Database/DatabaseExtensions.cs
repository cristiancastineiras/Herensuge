using System.Data.Common;
using Herensuge.Common.Application.Data;
using Herensuge.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Herensuge.Common.Infrastructure.Database;

/// <summary>Registered once per module DbContext so the host can migrate every schema.</summary>
public sealed record ModuleDatabase(Type DbContextType, string Schema);

public static class DatabaseExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        services.AddSingleton(new ModuleDatabase(typeof(TContext), schema));
        return services;
    }

    internal static IServiceCollection AddPostgres(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            string connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Database")
                ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");
            return new NpgsqlDataSourceBuilder(connectionString).Build();
        });

        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        return services;
    }

    private sealed class DbConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
    {
        public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
            await dataSource.OpenConnectionAsync(cancellationToken);
    }
}
