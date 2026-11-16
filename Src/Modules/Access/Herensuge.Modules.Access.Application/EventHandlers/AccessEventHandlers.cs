using Herensuge.Modules.Access.Contracts.IntegrationEvents;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers.Events;
using Herensuge.Modules.Access.Domain.Roles.Events;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
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
    IDomainEventHandler<RoleUnassignedDomainEvent>,
    IDomainEventHandler<GroupCreatedDomainEvent>,
    IDomainEventHandler<GroupUpdatedDomainEvent>,
    IDomainEventHandler<GroupDeletedDomainEvent>,
    IDomainEventHandler<GroupMembersChangedDomainEvent>,
    IDomainEventHandler<ServiceAccountCreatedDomainEvent>,
    IDomainEventHandler<ServiceAccountSecretRotatedDomainEvent>,
    IDomainEventHandler<ServiceAccountDeletedDomainEvent>,
    IDomainEventHandler<PlanChangedDomainEvent>,
    IDomainEventHandler<TenantPlanChangedDomainEvent>
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
            new RoleAssignedIntegrationEvent(
                domainEvent.TenantId,
                UserOf(domainEvent.SubjectType, domainEvent.SubjectId),
                domainEvent.SubjectType,
                domainEvent.SubjectId,
                domainEvent.RoleId,
                domainEvent.RoleTemplateId,
                domainEvent.ExpiresAtUtc),
            cancellationToken);

    public Task Handle(RoleUnassignedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new RoleUnassignedIntegrationEvent(
                domainEvent.TenantId,
                UserOf(domainEvent.SubjectType, domainEvent.SubjectId),
                domainEvent.SubjectType,
                domainEvent.SubjectId,
                domainEvent.RoleId,
                domainEvent.RoleTemplateId),
            cancellationToken);

    public Task Handle(GroupCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new GroupCreatedIntegrationEvent(domainEvent.TenantId, domainEvent.GroupId, domainEvent.Name), cancellationToken);

    public Task Handle(GroupUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new GroupUpdatedIntegrationEvent(domainEvent.TenantId, domainEvent.GroupId, domainEvent.Name), cancellationToken);

    public Task Handle(GroupDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new GroupDeletedIntegrationEvent(domainEvent.TenantId, domainEvent.GroupId, domainEvent.Name), cancellationToken);

    public Task Handle(GroupMembersChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new GroupMembersChangedIntegrationEvent(domainEvent.TenantId, domainEvent.GroupId, domainEvent.Added, domainEvent.Removed),
            cancellationToken);

    public Task Handle(ServiceAccountCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ServiceAccountCreatedIntegrationEvent(domainEvent.TenantId, domainEvent.ServiceAccountId, domainEvent.Name, domainEvent.ClientId),
            cancellationToken);

    public Task Handle(ServiceAccountSecretRotatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ServiceAccountSecretRotatedIntegrationEvent(domainEvent.TenantId, domainEvent.ServiceAccountId, domainEvent.ClientId),
            cancellationToken);

    public Task Handle(ServiceAccountDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ServiceAccountDeletedIntegrationEvent(domainEvent.TenantId, domainEvent.ServiceAccountId, domainEvent.Name, domainEvent.ClientId),
            cancellationToken);

    public Task Handle(PlanChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new PlanChangedIntegrationEvent(domainEvent.Key, domainEvent.Name, domainEvent.Features, domainEvent.Deleted), cancellationToken);

    public Task Handle(TenantPlanChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new TenantPlanChangedIntegrationEvent(domainEvent.TenantId, domainEvent.PlanKey), cancellationToken);

    private static Guid? UserOf(string subjectType, Guid subjectId) => subjectType == SubjectTypes.User ? subjectId : null;
}

/// <summary>A member who leaves (or is removed from) an organization loses every role and group there (idempotent, plan D6).</summary>
internal sealed class RemoveAccessOnMemberRemovedHandler(IRoleAssignmentRepository assignments, IGroupRepository groups, IUnitOfWork unitOfWork)
    : IIntegrationEventHandler<MemberRemovedIntegrationEvent>
{
    public async Task Handle(MemberRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        foreach (RoleAssignment assignment in await assignments.ListAsync(integrationEvent.TenantId, [integrationEvent.UserId], cancellationToken))
        {
            assignment.Revoke();
            assignments.Remove(assignment);
        }

        foreach (Group group in await groups.ListByMemberAsync(integrationEvent.TenantId, integrationEvent.UserId, cancellationToken))
        {
            group.RemoveMember(integrationEvent.UserId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>A deleted account loses every role and group in every organization (idempotent).</summary>
internal sealed class RemoveAccessOnUserDeletedHandler(IRoleAssignmentRepository assignments, IGroupRepository groups, IUnitOfWork unitOfWork)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        foreach (RoleAssignment assignment in await assignments.ListByUserAsync(integrationEvent.UserId, cancellationToken))
        {
            assignments.Remove(assignment);
        }

        foreach (Group group in await groups.ListByMemberAsync(integrationEvent.UserId, cancellationToken))
        {
            group.RemoveMember(integrationEvent.UserId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
