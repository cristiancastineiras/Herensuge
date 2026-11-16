using Herensuge.Modules.Access.Contracts.IntegrationEvents;
using Herensuge.Modules.Access.Domain.ResourceServers.Events;
using Herensuge.Modules.Access.Domain.Roles.Events;
using Herensuge.Modules.Identity.Contracts.IntegrationEvents;
using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Access.Application.EventHandlers;

/// <summary>Translates Access domain events into the integration events other modules listen to (Oidc, Audit).</summary>
internal sealed class AccessIntegrationEventPublisher(IEventBus eventBus) :
    IDomainEventHandler<ResourceServerSyncedDomainEvent>,
    IDomainEventHandler<RoleCreatedDomainEvent>,
    IDomainEventHandler<RoleUpdatedDomainEvent>,
    IDomainEventHandler<RoleDeletedDomainEvent>,
    IDomainEventHandler<RoleAssignedDomainEvent>,
    IDomainEventHandler<RoleUnassignedDomainEvent>
{
    public Task Handle(ResourceServerSyncedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ResourceServerSyncedIntegrationEvent(
                domainEvent.ResourceServerId, domainEvent.Key, domainEvent.Scope, domainEvent.Audience, domainEvent.ClientId, domainEvent.ManifestVersion),
            cancellationToken);

    public Task Handle(RoleCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new RoleCreatedIntegrationEvent(domainEvent.TenantId, domainEvent.RoleId, domainEvent.Name, domainEvent.PermissionIds), cancellationToken);

    public Task Handle(RoleUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new RoleUpdatedIntegrationEvent(domainEvent.TenantId, domainEvent.RoleId, domainEvent.Name, domainEvent.PermissionIds), cancellationToken);

    public Task Handle(RoleDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new RoleDeletedIntegrationEvent(domainEvent.TenantId, domainEvent.RoleId, domainEvent.Name), cancellationToken);

    public Task Handle(RoleAssignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new RoleAssignedIntegrationEvent(domainEvent.TenantId, domainEvent.UserId, domainEvent.RoleId, domainEvent.RoleTemplateId, domainEvent.ExpiresAtUtc),
            cancellationToken);

    public Task Handle(RoleUnassignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new RoleUnassignedIntegrationEvent(domainEvent.TenantId, domainEvent.UserId, domainEvent.RoleId, domainEvent.RoleTemplateId),
            cancellationToken);
}

/// <summary>A member who leaves (or is removed from) an organization loses every role there (idempotent).</summary>
internal sealed class RemoveAssignmentsOnMemberRemovedHandler(IRoleAssignmentRepository assignments, IUnitOfWork unitOfWork)
    : IIntegrationEventHandler<MemberRemovedIntegrationEvent>
{
    public async Task Handle(MemberRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        foreach (RoleAssignment assignment in await assignments.ListAsync(integrationEvent.TenantId, integrationEvent.UserId, cancellationToken))
        {
            assignment.Revoke();
            assignments.Remove(assignment);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>A deleted account loses every role in every organization (idempotent).</summary>
internal sealed class RemoveAssignmentsOnUserDeletedHandler(IRoleAssignmentRepository assignments, IUnitOfWork unitOfWork)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        foreach (RoleAssignment assignment in await assignments.ListByUserAsync(integrationEvent.UserId, cancellationToken))
        {
            assignments.Remove(assignment);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
