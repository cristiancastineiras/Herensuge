using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;
using Herensuge.Modules.Tenancy.Domain.Invitations.Events;
using Herensuge.Modules.Tenancy.Domain.Memberships.Events;
using Herensuge.Modules.Tenancy.Domain.Tenants.Events;

namespace Herensuge.Modules.Tenancy.Application.EventHandlers;

/// <summary>Translates Tenancy domain events into the integration events other modules listen to.</summary>
internal sealed class TenancyIntegrationEventPublisher(IEventBus eventBus) :
    IDomainEventHandler<TenantCreatedDomainEvent>,
    IDomainEventHandler<TenantUpdatedDomainEvent>,
    IDomainEventHandler<TenantStatusChangedDomainEvent>,
    IDomainEventHandler<MemberAddedDomainEvent>,
    IDomainEventHandler<MemberRoleChangedDomainEvent>,
    IDomainEventHandler<MemberRemovedDomainEvent>,
    IDomainEventHandler<InvitationCreatedDomainEvent>,
    IDomainEventHandler<InvitationAcceptedDomainEvent>,
    IDomainEventHandler<InvitationRevokedDomainEvent>
{
    public Task Handle(TenantCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new TenantCreatedIntegrationEvent(domainEvent.TenantId, domainEvent.Name, domainEvent.Slug, domainEvent.CreatedByUserId),
            cancellationToken);

    public Task Handle(TenantUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new TenantUpdatedIntegrationEvent(domainEvent.TenantId, domainEvent.Name, domainEvent.RequireMfa),
            cancellationToken);

    public Task Handle(TenantStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new TenantStatusChangedIntegrationEvent(domainEvent.TenantId, domainEvent.IsActive), cancellationToken);

    public Task Handle(MemberAddedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new MemberAddedIntegrationEvent(domainEvent.TenantId, domainEvent.UserId, domainEvent.Role.ToClaimValue()),
            cancellationToken);

    public Task Handle(MemberRoleChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new MemberRoleChangedIntegrationEvent(
                domainEvent.TenantId,
                domainEvent.UserId,
                domainEvent.PreviousRole.ToClaimValue(),
                domainEvent.NewRole.ToClaimValue()),
            cancellationToken);

    public Task Handle(MemberRemovedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new MemberRemovedIntegrationEvent(domainEvent.TenantId, domainEvent.UserId), cancellationToken);

    public Task Handle(InvitationCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new InvitationCreatedIntegrationEvent(
                domainEvent.TenantId,
                domainEvent.InvitationId,
                domainEvent.TenantName,
                domainEvent.Email,
                domainEvent.Role.ToClaimValue(),
                domainEvent.InvitedByUserId,
                domainEvent.Token,
                domainEvent.ExpiresAtUtc),
            cancellationToken);

    public Task Handle(InvitationAcceptedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new InvitationAcceptedIntegrationEvent(domainEvent.TenantId, domainEvent.InvitationId, domainEvent.UserId),
            cancellationToken);

    public Task Handle(InvitationRevokedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new InvitationRevokedIntegrationEvent(domainEvent.TenantId, domainEvent.InvitationId), cancellationToken);
}
