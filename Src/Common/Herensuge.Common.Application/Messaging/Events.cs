using Herensuge.Common.Domain;

namespace Herensuge.Common.Application.Messaging;

public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Event published to other modules through the outbox → inbox pipeline.</summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>Marks integration events that belong to one organization (used by the audit log).</summary>
public interface ITenantIntegrationEvent
{
    Guid TenantId { get; }
}

public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{
    Task Handle(TIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

public interface IEventBus
{
    Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TIntegrationEvent : IntegrationEvent;
}

/// <summary>Properties carrying secrets (links, codes). They are never written to the audit log.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
}
