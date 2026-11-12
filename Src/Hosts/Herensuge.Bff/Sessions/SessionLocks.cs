using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Herensuge.Bff.Sessions;

/// <summary>
/// Serializes token refreshes per session: two parallel refreshes would trigger OpenIddict's refresh
/// token reuse detection and revoke the whole chain.
/// </summary>
internal interface ISessionLock
{
    Task<IAsyncDisposable?> AcquireAsync(string sessionKey, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Distributed lock in Valkey (works across BFF instances).</summary>
internal sealed class ValkeySessionLock(IConnectionMultiplexer multiplexer) : ISessionLock
{
    private static readonly TimeSpan LockLifetime = TimeSpan.FromSeconds(15);

    public async Task<IAsyncDisposable?> AcquireAsync(string sessionKey, TimeSpan timeout, CancellationToken cancellationToken)
    {
        IDatabase database = multiplexer.GetDatabase();
        RedisKey key = $"herensuge-bff:refresh-lock:{sessionKey}";
        RedisValue token = Guid.NewGuid().ToString("N");
        DateTime deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await database.LockTakeAsync(key, token, LockLifetime))
            {
                return new Release(database, key, token);
            }

            await Task.Delay(100, cancellationToken);
        }

        return null;
    }

    private sealed class Release(IDatabase database, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await database.LockReleaseAsync(key, token);
    }
}

/// <summary>Development without Valkey: single-instance lock.</summary>
internal sealed class InMemorySessionLock : ISessionLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable?> AcquireAsync(string sessionKey, TimeSpan timeout, CancellationToken cancellationToken)
    {
        SemaphoreSlim semaphore = _locks.GetOrAdd(sessionKey, _ => new SemaphoreSlim(1, 1));
        return await semaphore.WaitAsync(timeout, cancellationToken) ? new Release(semaphore) : null;
    }

    private sealed class Release(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
