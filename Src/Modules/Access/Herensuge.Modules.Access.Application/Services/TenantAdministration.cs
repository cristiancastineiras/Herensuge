using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Services;

/// <summary>
/// Roles and assignments are managed by owners and admins, checked against the database (the tenant_role
/// claim may be five minutes old).
/// </summary>
// shortcut: base roles decide who administers access; becomes the herensuge:roles:manage permission when Herensuge registers itself as an application (plan, section 6.4).
public sealed class TenantAdministration(ITenancyModuleApi tenancy)
{
    public async Task<Result> RequireAdministratorAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken)
    {
        MembershipSnapshot? actor = await tenancy.GetMembershipAsync(tenantId, actorUserId, cancellationToken);
        return actor is { TenantIsActive: true, Role: BaseRoles.Owner or BaseRoles.Admin } ? Result.Success() : AccessErrors.NotAllowed;
    }
}
