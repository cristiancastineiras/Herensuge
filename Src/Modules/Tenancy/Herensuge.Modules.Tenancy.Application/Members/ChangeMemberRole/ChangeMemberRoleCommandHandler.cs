using Herensuge.Modules.Tenancy.Application.Services;

namespace Herensuge.Modules.Tenancy.Application.Members.ChangeMemberRole;

internal sealed class ChangeMemberRoleCommandHandler(
    TenantAccess tenantAccess,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork) : ICommandHandler<ChangeMemberRoleCommand>
{
    public async Task<Result> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
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

        Membership? target = await memberships.GetAsync(command.TenantId, command.UserId, cancellationToken);
        if (target is null)
        {
            return MembershipErrors.NotFound;
        }

        int owners = await memberships.CountOwnersAsync(command.TenantId, cancellationToken);
        Result changed = target.ChangeRole(role, access.Value.Actor.Role, owners);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
