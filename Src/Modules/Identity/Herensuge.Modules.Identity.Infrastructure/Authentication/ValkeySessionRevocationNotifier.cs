using Herensuge.Modules.Identity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Herensuge.Modules.Identity.Infrastructure.Authentication;

/// <summary>Publishes the sid on a Valkey channel; every Herensuge.Bff instance listens to it (SessionRevocationListener).</summary>
internal sealed class ValkeySessionRevocationNotifier(IServiceProvider services) : ISessionRevocationNotifier
{
    public const string Channel = "herensuge:session-revoked";

    // Development without Valkey: nobody to tell. The per-request session checks still cut the access.
    public Task NotifyAsync(Guid sessionId, CancellationToken cancellationToken) =>
        services.GetService<IConnectionMultiplexer>() is { } valkey
            ? valkey.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), sessionId.ToString())
            : Task.CompletedTask;
}
