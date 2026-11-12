using Herensuge.Modules.Identity.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Tenancy.Application.EventHandlers;

/// <summary>When an account is deleted its memberships go too (idempotent: nothing left on a retry).</summary>
internal sealed class RemoveMembershipsOnUserDeletedHandler(IMembershipRepository memberships, IUnitOfWork unitOfWork)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> userMemberships = await memberships.GetByUserAsync(integrationEvent.UserId, cancellationToken);
        foreach (Membership membership in userMemberships)
        {
            membership.RemoveBecauseUserWasDeleted();
            memberships.Remove(membership);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
