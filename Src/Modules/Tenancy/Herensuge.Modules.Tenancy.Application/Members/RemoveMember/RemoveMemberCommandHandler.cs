using Herensuge.Modules.Tenancy.Application.Services;

namespace Herensuge.Modules.Tenancy.Application.Members.RemoveMember;

internal sealed class RemoveMemberCommandHandler(
    TenantAccess tenantAccess,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveMemberCommand>
{
    public async Task<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        Membership? target = await memberships.GetAsync(command.TenantId, command.UserId, cancellationToken);
        if (target is null)
        {
            return MembershipErrors.NotFound;
        }

        TenantRole? actorRole = null;
        if (command.ActorUserId != command.UserId)
        {
            Result<(Tenant Tenant, Membership Actor)> access =
                await tenantAccess.RequireAdministratorAsync(command.TenantId, command.ActorUserId, cancellationToken);
            if (access.IsFailure)
            {
                return access.Error;
            }

            actorRole = access.Value.Actor.Role;
        }

        int owners = await memberships.CountOwnersAsync(command.TenantId, cancellationToken);
        Result removed = target.Remove(actorRole, owners);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        memberships.Remove(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
