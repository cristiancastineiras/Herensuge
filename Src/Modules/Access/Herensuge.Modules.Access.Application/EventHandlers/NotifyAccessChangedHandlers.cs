using Herensuge.Modules.Access.Domain.Roles.Events;
using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Access.Application.EventHandlers;

/// <summary>Bumps the access versions after each change (plan D4). Runs from the outbox, a second or two later.</summary>
internal sealed class NotifyAccessChangedHandler(IAccessChangeNotifier notifier) :
    IDomainEventHandler<RoleAssignedDomainEvent>,
    IDomainEventHandler<RoleUnassignedDomainEvent>,
    IDomainEventHandler<RoleUpdatedDomainEvent>,
    IDomainEventHandler<RoleDeletedDomainEvent>
{
    public Task Handle(RoleAssignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(domainEvent.TenantId, domainEvent.UserId, cancellationToken);

    public Task Handle(RoleUnassignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(domainEvent.TenantId, domainEvent.UserId, cancellationToken);

    public Task Handle(RoleUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);

    public Task Handle(RoleDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);
}

/// <summary>A base-role change changes which application roles the member holds (grantTo).</summary>
internal sealed class NotifyAccessChangedOnMemberRoleChangedHandler(IAccessChangeNotifier notifier)
    : IIntegrationEventHandler<MemberRoleChangedIntegrationEvent>
{
    public Task Handle(MemberRoleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(integrationEvent.TenantId, integrationEvent.UserId, cancellationToken);
}
