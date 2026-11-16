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

        Tenant tenant = access.Value.Tenant;
        tenant.Update(command.Name, command.RequireMfa, command.TrustedNetworks ?? tenant.TrustedNetworks);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
