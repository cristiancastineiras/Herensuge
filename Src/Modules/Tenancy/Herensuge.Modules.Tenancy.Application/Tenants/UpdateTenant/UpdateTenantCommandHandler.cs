using Herensuge.Modules.Tenancy.Application.Services;

namespace Herensuge.Modules.Tenancy.Application.Tenants.UpdateTenant;

internal sealed class UpdateTenantCommandHandler(TenantAccess tenantAccess, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTenantCommand>
{
    public async Task<Result> Handle(UpdateTenantCommand command, CancellationToken cancellationToken)
    {
        Result<(Tenant Tenant, Membership Actor)> access =
            await tenantAccess.RequireAdministratorAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        access.Value.Tenant.Update(command.Name, command.RequireMfa);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
