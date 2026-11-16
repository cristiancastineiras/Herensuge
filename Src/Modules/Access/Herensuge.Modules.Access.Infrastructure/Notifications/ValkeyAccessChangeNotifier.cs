using Herensuge.Modules.Access.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Herensuge.Modules.Access.Infrastructure.Notifications;

/// <summary>
/// Monotonic counters in Valkey: herensuge:access-version:{tenant} and herensuge:access-version:{tenant}:{user}.
/// Every BFF reads them on its heartbeat (Herensuge.Bff, AccessVersions) and refreshes once when they change.
/// </summary>
internal sealed class ValkeyAccessChangeNotifier(IServiceProvider services) : IAccessChangeNotifier
{
    public const string Prefix = "herensuge:access-version:";

    // Development without Valkey: no BFF to tell. Tokens catch up on their next refresh.
    public Task TenantChangedAsync(Guid tenantId, CancellationToken cancellationToken) => IncrementAsync($"{Prefix}{tenantId}");

    public Task UserChangedAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => IncrementAsync($"{Prefix}{tenantId}:{userId}");

    private Task IncrementAsync(string key) =>
        services.GetService<IConnectionMultiplexer>() is { } valkey ? valkey.GetDatabase().StringIncrementAsync(key) : Task.CompletedTask;
}
