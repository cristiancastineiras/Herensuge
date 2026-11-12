using Herensuge.Modules.Tenancy.Application.Services;

namespace Herensuge.Modules.Tenancy.Application.Invitations.RevokeInvitation;

internal sealed class RevokeInvitationCommandHandler(
    TenantAccess tenantAccess,
    IInvitationRepository invitations,
    IUnitOfWork unitOfWork) : ICommandHandler<RevokeInvitationCommand>
{
    public async Task<Result> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        Result<(Tenant Tenant, Membership Actor)> access =
            await tenantAccess.RequireAdministratorAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        Invitation? invitation = await invitations.GetByIdAsync(command.InvitationId, cancellationToken);
        if (invitation is null || invitation.TenantId != command.TenantId)
        {
            return InvitationErrors.NotFound;
        }

        Result revoked = invitation.Revoke();
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
