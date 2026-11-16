using StackExchange.Redis;

namespace Herensuge.Bff.Sessions;

/// <summary>Raised on this instance when the IdP reports that an SSO session (sid) has ended. /bff/events listens to it.</summary>
internal sealed class SessionEndedSignal
{
    public event Action<string>? Ended;

    public void Raise(string sid) => Ended?.Invoke(sid);
}

/// <summary>
/// Herensuge.Api publishes the sid of every closed session on a Valkey channel (sign-out, closed from
/// another device, password changed...). Each BFF instance drops its session for that sid and tells the
/// open tabs, so closing a session takes effect in real time instead of at the next token refresh.
/// </summary>
internal sealed class SessionRevocationListener(
    IConnectionMultiplexer multiplexer,
    DistributedTicketStore tickets,
    SessionEndedSignal signal,
    ILogger<SessionRevocationListener> logger) : IHostedService
{
    // Same channel as ValkeySessionRevocationNotifier (Identity module).
    private static readonly RedisChannel Channel = RedisChannel.Literal("herensuge:session-revoked");

    // FireAndForget: the BFF starts even if Valkey is not reachable yet; the subscription is restored on (re)connect.
    public async Task StartAsync(CancellationToken cancellationToken) =>
        (await multiplexer.GetSubscriber().SubscribeAsync(Channel, CommandFlags.FireAndForget)).OnMessage(async message =>
        {
            string sid = message.Message.ToString();
            try
            {
                await tickets.RemoveBySessionIdAsync(sid);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                // The API already rejects the tokens of that session and the next refresh removes it.
                logger.LogWarning(exception, "The BFF session of a closed SSO session could not be removed");
            }

            signal.Raise(sid);
        });

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
