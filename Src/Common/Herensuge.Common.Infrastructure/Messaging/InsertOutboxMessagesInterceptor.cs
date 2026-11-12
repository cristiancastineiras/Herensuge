using System.Diagnostics;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Herensuge.Common.Infrastructure.Messaging;

/// <summary>Writes the domain events raised by tracked aggregates into the outbox, inside the same transaction.</summary>
public sealed class InsertOutboxMessagesInterceptor(IRequestContext requestContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            InsertOutboxMessages(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            InsertOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void InsertOutboxMessages(DbContext context)
    {
        List<IDomainEvent> domainEvents =
        [
            .. context.ChangeTracker
                .Entries<AggregateRoot>()
                .SelectMany(entry =>
                {
                    IReadOnlyCollection<IDomainEvent> events = entry.Entity.GetDomainEvents();
                    entry.Entity.ClearDomainEvents();
                    return events;
                }),
        ];

        if (domainEvents.Count == 0)
        {
            return;
        }

        string? traceParent = Activity.Current?.Id;

        context.Set<OutboxMessage>().AddRange(domainEvents.Select(domainEvent => new OutboxMessage
        {
            Id = domainEvent.Id,
            Type = MessageSerializer.GetTypeName(domainEvent.GetType()),
            Content = MessageSerializer.Serialize(domainEvent),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
            CorrelationId = requestContext.CorrelationId,
            TraceParent = traceParent,
            ActorUserId = requestContext.ActorUserId,
            IpAddress = requestContext.IpAddress,
            UserAgent = requestContext.UserAgent,
        }));
    }
}
