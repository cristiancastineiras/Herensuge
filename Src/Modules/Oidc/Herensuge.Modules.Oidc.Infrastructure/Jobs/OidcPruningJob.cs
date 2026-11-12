using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Herensuge.Modules.Oidc.Infrastructure.Jobs;

/// <summary>Hourly removal of expired, redeemed or revoked tokens and authorizations older than one day.</summary>
internal sealed class OidcPruningJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OidcPruningJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                DateTimeOffset threshold = timeProvider.GetUtcNow().AddDays(-1);

                long tokens = await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().PruneAsync(threshold, stoppingToken);
                long authorizations = await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().PruneAsync(threshold, stoppingToken);

                if (tokens + authorizations > 0)
                {
                    logger.LogInformation("Pruned {Tokens} tokens and {Authorizations} authorizations", tokens, authorizations);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Pruning OIDC entries failed; it will run again in {Interval}", Interval);
            }
        }
    }
}
