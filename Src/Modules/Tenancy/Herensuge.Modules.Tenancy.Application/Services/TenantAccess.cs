namespace Herensuge.Modules.Tenancy.Application.Services;

/// <summary>
/// Authorization of organization changes against the database (the role claim in the access token
/// may be up to five minutes old).
/// </summary>
public sealed class TenantAccess(ITenantRepository tenants, IMembershipRepository memberships)
{
    public async Task<Result<(Tenant Tenant, Membership Actor)>> RequireAdministratorAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        Tenant? tenant = await tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantErrors.NotFound;
        }

        if (!tenant.IsActive)
        {
            return TenantErrors.Inactive;
        }

        Membership? actor = await memberships.GetAsync(tenantId, actorUserId, cancellationToken);
        if (actor is null || !actor.Role.IsAdministrator())
        {
            return MembershipErrors.NotAllowed;
        }

        return (tenant, actor);
    }
}
