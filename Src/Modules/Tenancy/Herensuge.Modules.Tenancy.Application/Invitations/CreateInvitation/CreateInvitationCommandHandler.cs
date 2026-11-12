using Herensuge.Modules.Identity.Contracts;
using Herensuge.Modules.Tenancy.Application.Services;

namespace Herensuge.Modules.Tenancy.Application.Invitations.CreateInvitation;

/// <summary>Creates (or re-sends) an invitation. A previous pending invitation for the same email is revoked.</summary>
internal sealed class CreateInvitationCommandHandler(
    TenantAccess tenantAccess,
    IMembershipRepository memberships,
    IInvitationRepository invitations,
    IIdentityModuleApi identity,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<CreateInvitationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        if (!TenantRoleRules.TryParse(command.Role, out TenantRole role))
        {
            return MembershipErrors.InvalidRole;
        }

        Result<(Tenant Tenant, Membership Actor)> access =
            await tenantAccess.RequireAdministratorAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        if (!access.Value.Actor.Role.CanAssign(role))
        {
            return MembershipErrors.NotAllowed;
        }

        string email = command.Email.Trim().ToLowerInvariant();
        Guid? existingUserId = await identity.FindUserIdByEmailAsync(email, cancellationToken);
        if (existingUserId is { } userId && await memberships.GetAsync(command.TenantId, userId, cancellationToken) is not null)
        {
            return MembershipErrors.AlreadyMember;
        }

        foreach (Invitation pending in await invitations.GetPendingByEmailAsync(command.TenantId, email, cancellationToken))
        {
            pending.Revoke();
        }

        (Invitation invitation, _) = Invitation.Create(
            command.TenantId,
            access.Value.Tenant.Name,
            email,
            role,
            command.ActorUserId,
            hasher,
            timeProvider.GetUtcNow().UtcDateTime);

        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return invitation.Id;
    }
}
