using Herensuge.Common.Application.Messaging;
using Herensuge.Modules.Identity.Application.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Herensuge.Modules.Identity.Infrastructure.Jobs;

/// <summary>
/// Every minute, ends the support sessions past their 30 minutes (plan D11). They are already inactive at that point:
/// ending them only emits the events (audit, BFF logout).
/// </summary>
internal sealed partial class SupportSessionJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SupportSessionJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICommandHandler<EndExpiredSupportSessionsCommand, int>>()
                    .Handle(new EndExpiredSupportSessionsCommand(), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ending expired support sessions failed; retrying in a minute")]
    private partial void LogFailed(Exception exception);
}
