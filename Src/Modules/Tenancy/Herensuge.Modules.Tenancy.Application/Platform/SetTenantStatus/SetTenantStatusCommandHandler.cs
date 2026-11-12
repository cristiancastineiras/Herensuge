namespace Herensuge.Modules.Tenancy.Application.Platform.SetTenantStatus;

internal sealed class SetTenantStatusCommandHandler(ITenantRepository tenants, IUnitOfWork unitOfWork)
    : ICommandHandler<SetTenantStatusCommand>
{
    public async Task<Result> Handle(SetTenantStatusCommand command, CancellationToken cancellationToken)
    {
        Tenant? tenant = await tenants.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantErrors.NotFound;
        }

        tenant.SetActive(command.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
