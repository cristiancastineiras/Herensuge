using System.Diagnostics;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Herensuge.Common.Infrastructure.Messaging;

/// <summary>
/// Writes the domain events raised by tracked aggregates into the outbox, inside the same transaction. The content
/// (personal data, links, codes), IP and user agent are encrypted.
/// </summary>
public sealed class InsertOutboxMessagesInterceptor(IRequestContext requestContext, IDataEncryptor encryptor) : SaveChangesInterceptor
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
        string schema = context.Model.GetDefaultSchema() ?? "public";
        string Purpose(string column) => MessageEncryption.Purpose(schema, MessageEncryption.OutboxTable, column);

        context.Set<OutboxMessage>().AddRange(domainEvents.Select(domainEvent => new OutboxMessage
        {
            Id = domainEvent.Id,
            Type = MessageSerializer.GetTypeName(domainEvent.GetType()),
            Content = encryptor.Encrypt(MessageSerializer.Serialize(domainEvent), Purpose("content")),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
            CorrelationId = requestContext.CorrelationId,
            TraceParent = traceParent,
            ActorUserId = requestContext.ActorUserId,
            IpAddress = encryptor.EncryptNullable(requestContext.IpAddress, Purpose("ip_address")),
            UserAgent = encryptor.EncryptNullable(requestContext.UserAgent, Purpose("user_agent")),
        }));
    }
}
