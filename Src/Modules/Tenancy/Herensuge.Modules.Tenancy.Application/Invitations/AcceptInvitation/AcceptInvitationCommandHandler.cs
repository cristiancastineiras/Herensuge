using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Tenancy.Application.Invitations.AcceptInvitation;

internal sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitations,
    ITenantRepository tenants,
    IMembershipRepository memberships,
    IIdentityModuleApi identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AcceptInvitationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        Invitation? invitation = await invitations.GetByTokenHashAsync(Invitation.HashToken(command.Token), cancellationToken);
        if (invitation is null)
        {
            return InvitationErrors.NotFound;
        }

        Tenant? tenant = await tenants.GetByIdAsync(invitation.TenantId, cancellationToken);
        if (tenant is not { IsActive: true })
        {
            return TenantErrors.Inactive;
        }

        UserSnapshot? user = await identity.GetUserAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return MembershipErrors.NotFound;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        Result accepted = invitation.Accept(user.Id, user.Email, user.EmailVerified, now);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        if (await memberships.GetAsync(tenant.Id, user.Id, cancellationToken) is null)
        {
            memberships.Add(Membership.Create(tenant.Id, user.Id, invitation.Role, now));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }
}
