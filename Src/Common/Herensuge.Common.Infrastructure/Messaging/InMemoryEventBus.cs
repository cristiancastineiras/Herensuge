using System.Diagnostics;
using Dapper;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Application.Messaging;
using Npgsql;

namespace Herensuge.Common.Infrastructure.Messaging;

/// <summary>
/// In-process bus: an integration event is written into the inbox of every module that handles it.
/// Swapping this for a RabbitMQ implementation of <see cref="IEventBus"/> leaves handlers untouched.
/// </summary>
internal sealed class InMemoryEventBus(
    NpgsqlDataSource dataSource,
    IEnumerable<MessagingModule> modules,
    IRequestContext requestContext) : IEventBus
{
    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TIntegrationEvent : IntegrationEvent
    {
        Type eventType = integrationEvent.GetType();
        MessagingModule[] targets = [.. modules.Where(module => module.HandlesIntegrationEvent(eventType))];
        if (targets.Length == 0)
        {
            return;
        }

        var row = new
        {
            integrationEvent.Id,
            Type = MessageSerializer.GetTypeName(eventType),
            Content = MessageSerializer.Serialize(integrationEvent),
            OccurredOnUtc = DateTime.SpecifyKind(integrationEvent.OccurredOnUtc, DateTimeKind.Utc),
            requestContext.CorrelationId,
            TraceParent = Activity.Current?.Id,
            requestContext.ActorUserId,
            requestContext.IpAddress,
            requestContext.UserAgent,
        };

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (MessagingModule module in targets)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                INSERT INTO {module.Schema}.inbox_messages
                    (id, type, content, occurred_on_utc, attempts, correlation_id, trace_parent, actor_user_id, ip_address, user_agent)
                VALUES
                    (@Id, @Type, @Content::jsonb, @OccurredOnUtc, 0, @CorrelationId, @TraceParent, @ActorUserId, @IpAddress, @UserAgent)
                ON CONFLICT (id) DO NOTHING
                """,
                row,
                cancellationToken: cancellationToken));
        }
    }
}
