using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.Roles.Events;
using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Access.Application.EventHandlers;

/// <summary>
/// Bumps the access versions after each change (plan D4). Runs from the outbox, a second or two later. Service
/// accounts have no session to update: their tokens last 5 minutes.
/// </summary>
// shortcut: editing a plan's features does not bump the organizations on it (their tokens catch up within 5 minutes); bump them if plans change often.
internal sealed class NotifyAccessChangedHandler(IAccessChangeNotifier notifier) :
    IDomainEventHandler<RoleAssignedDomainEvent>,
    IDomainEventHandler<RoleUnassignedDomainEvent>,
    IDomainEventHandler<RoleUpdatedDomainEvent>,
    IDomainEventHandler<RoleDeletedDomainEvent>,
    IDomainEventHandler<GroupMembersChangedDomainEvent>,
    IDomainEventHandler<GroupDeletedDomainEvent>,
    IDomainEventHandler<TenantPlanChangedDomainEvent>
{
    public Task Handle(RoleAssignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        SubjectChangedAsync(domainEvent.TenantId, domainEvent.SubjectType, domainEvent.SubjectId, cancellationToken);

    public Task Handle(RoleUnassignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        SubjectChangedAsync(domainEvent.TenantId, domainEvent.SubjectType, domainEvent.SubjectId, cancellationToken);

    public Task Handle(RoleUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);

    public Task Handle(RoleDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);

    public async Task Handle(GroupMembersChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        foreach (Guid userId in domainEvent.Added.Concat(domainEvent.Removed))
        {
            await notifier.UserChangedAsync(domainEvent.TenantId, userId, cancellationToken);
        }
    }

    public Task Handle(GroupDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);

    public Task Handle(TenantPlanChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(domainEvent.TenantId, cancellationToken);

    /// <summary>A member: their counter; a group: the whole organization (any member may be in it).</summary>
    private Task SubjectChangedAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken) => subjectType switch
    {
        SubjectTypes.User => notifier.UserChangedAsync(tenantId, subjectId, cancellationToken),
        SubjectTypes.Group => notifier.TenantChangedAsync(tenantId, cancellationToken),
        _ => Task.CompletedTask,
    };
}

/// <summary>A base-role change changes which application roles the member holds (grantTo).</summary>
internal sealed class NotifyAccessChangedOnMemberRoleChangedHandler(IAccessChangeNotifier notifier)
    : IIntegrationEventHandler<MemberRoleChangedIntegrationEvent>
{
    public Task Handle(MemberRoleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(integrationEvent.TenantId, integrationEvent.UserId, cancellationToken);
}

/// <summary>Trusted networks are part of the decision for trusted_network permissions (plan D9).</summary>
internal sealed class NotifyAccessChangedOnTenantUpdatedHandler(IAccessChangeNotifier notifier)
    : IIntegrationEventHandler<TenantUpdatedIntegrationEvent>
{
    public Task Handle(TenantUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        notifier.TenantChangedAsync(integrationEvent.TenantId, cancellationToken);
}
