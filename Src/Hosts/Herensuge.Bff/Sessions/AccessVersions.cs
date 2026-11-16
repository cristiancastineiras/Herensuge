using StackExchange.Redis;

namespace Herensuge.Bff.Sessions;

/// <summary>
/// Access versions that Herensuge bumps in Valkey when roles, assignments or plans change (plan D4,
/// ValkeyAccessChangeNotifier). The heartbeat of /bff/events compares them with the ones saved in the session and
/// refreshes once when they differ. Without Valkey (development) there are no versions and no notice.
/// </summary>
internal sealed class AccessVersions(IServiceProvider services)
{
    public const string SessionItem = ".herensuge.access_versions";

    private const string Prefix = "herensuge:access-version:";

    /// <returns>"{organization}:{user}" counters, or null without Valkey or without an organization.</returns>
    public async Task<string?> ReadAsync(string? tenantId, string? userId)
    {
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId) || services.GetService<IConnectionMultiplexer>() is not { } valkey)
        {
            return null;
        }

        try
        {
            RedisValue[] values = await valkey.GetDatabase().StringGetAsync([$"{Prefix}{tenantId}", $"{Prefix}{tenantId}:{userId}"]);
            return $"{(long?)values[0] ?? 0}:{(long?)values[1] ?? 0}";
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // Only a hint for the UI: the claims catch up on the next refresh.
            return null;
        }
    }
}
