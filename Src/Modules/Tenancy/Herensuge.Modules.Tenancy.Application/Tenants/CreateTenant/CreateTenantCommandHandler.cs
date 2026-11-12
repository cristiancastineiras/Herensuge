using System.Security.Cryptography;

namespace Herensuge.Modules.Tenancy.Application.Tenants.CreateTenant;

internal sealed class CreateTenantCommandHandler(
    ITenantRepository tenants,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateTenantCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        TenantSlug slug = TenantSlug.FromName(command.Name);
        while (await tenants.SlugExistsAsync(slug.Value, cancellationToken))
        {
            slug = TenantSlug.FromName(command.Name).WithSuffix(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(2)));
        }

        var tenant = Tenant.Create(command.Name, slug, command.UserId, now);
        tenants.Add(tenant);
        memberships.Add(Membership.Create(tenant.Id, command.UserId, TenantRole.Owner, now));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }
}
