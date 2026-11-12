using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Herensuge.Modules.Identity.Infrastructure.Jobs;

/// <summary>Hourly purge of dead rows: used or expired tokens (after 1 day) and finished sessions (after 30 days).</summary>
internal sealed class IdentityCleanupJob(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<IdentityCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                DateTime now = timeProvider.GetUtcNow().UtcDateTime;
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(stoppingToken);

                int tokens = await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM identity.one_time_tokens WHERE expires_at_utc < @Cutoff",
                    new { Cutoff = now.AddDays(-1) },
                    cancellationToken: stoppingToken));

                int sessions = await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM identity.user_sessions WHERE COALESCE(revoked_at_utc, expires_at_utc) < @Cutoff",
                    new { Cutoff = now.AddDays(-30) },
                    cancellationToken: stoppingToken));

                if (tokens + sessions > 0)
                {
                    logger.LogInformation("Identity cleanup removed {Tokens} tokens and {Sessions} sessions", tokens, sessions);
                }
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                logger.LogWarning(exception, "Identity cleanup failed; it will run again in {Interval}", Interval);
            }
        }
    }
}
