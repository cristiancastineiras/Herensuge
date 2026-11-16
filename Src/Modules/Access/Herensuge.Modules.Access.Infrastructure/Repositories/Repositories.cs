using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Access.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Access.Infrastructure.Repositories;

internal sealed class ResourceServerRepository(AccessDbContext context) : IResourceServerRepository
{
    // Permissions, role templates and features are always loaded: split queries avoid a cartesian product.
    private IQueryable<ResourceServer> Servers => context.ResourceServers.AsSplitQuery();

    public Task<ResourceServer?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        Servers.SingleOrDefaultAsync(server => server.Key == key, cancellationToken);

    public Task<ResourceServer?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken) =>
        Servers.SingleOrDefaultAsync(server => server.ClientId == clientId, cancellationToken);

    public async Task<IReadOnlyList<ResourceServer>> ListAsync(CancellationToken cancellationToken) =>
        await Servers.ToListAsync(cancellationToken);

    public void Add(ResourceServer server) => context.ResourceServers.Add(server);
}

internal sealed class RoleRepository(AccessDbContext context) : IRoleRepository
{
    public Task<Role?> GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        context.Roles.SingleOrDefaultAsync(role => role.TenantId == tenantId && role.Id == roleId, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.Roles.Where(role => role.TenantId == tenantId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetManyAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        await context.Roles.Where(role => role.TenantId == tenantId && roleIds.Contains(role.Id)).ToListAsync(cancellationToken);

    public void Add(Role role) => context.Roles.Add(role);

    public void Remove(Role role) => context.Roles.Remove(role);
}

internal sealed class RoleAssignmentRepository(AccessDbContext context) : IRoleAssignmentRepository
{
    public async Task<IReadOnlyList<RoleAssignment>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await context.RoleAssignments
            .Where(assignment => assignment.TenantId == tenantId && subjectIds.Contains(assignment.SubjectId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoleAssignment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.RoleAssignments
            .Where(assignment => assignment.SubjectType == SubjectTypes.User && assignment.SubjectId == userId)
            .ToListAsync(cancellationToken);

    public void Add(RoleAssignment assignment) => context.RoleAssignments.Add(assignment);

    public void Remove(RoleAssignment assignment) => context.RoleAssignments.Remove(assignment);
}

internal sealed class GroupRepository(AccessDbContext context) : IGroupRepository
{
    public Task<Group?> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken) =>
        context.Groups.SingleOrDefaultAsync(group => group.TenantId == tenantId && group.Id == groupId, cancellationToken);

    public async Task<IReadOnlyList<Group>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.Groups.Where(group => group.TenantId == tenantId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Group>> ListByMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await context.Groups.Where(group => group.TenantId == tenantId && group.MemberUserIds.Contains(userId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Group>> ListByMemberAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Groups.Where(group => group.MemberUserIds.Contains(userId)).ToListAsync(cancellationToken);

    public void Add(Group group) => context.Groups.Add(group);

    public void Remove(Group group) => context.Groups.Remove(group);
}

internal sealed class ServiceAccountRepository(AccessDbContext context) : IServiceAccountRepository
{
    public Task<ServiceAccount?> GetAsync(Guid tenantId, Guid serviceAccountId, CancellationToken cancellationToken) =>
        context.ServiceAccounts.SingleOrDefaultAsync(account => account.TenantId == tenantId && account.Id == serviceAccountId, cancellationToken);

    public Task<ServiceAccount?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken) =>
        context.ServiceAccounts.SingleOrDefaultAsync(account => account.ClientId == clientId, cancellationToken);

    public Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.ServiceAccounts.CountAsync(account => account.TenantId == tenantId, cancellationToken);

    public void Add(ServiceAccount account) => context.ServiceAccounts.Add(account);

    public void Remove(ServiceAccount account) => context.ServiceAccounts.Remove(account);
}

internal sealed class PlanRepository(AccessDbContext context) : IPlanRepository
{
    public async Task<IReadOnlyList<Plan>> ListAsync(CancellationToken cancellationToken) =>
        await context.Plans.ToListAsync(cancellationToken);

    public Task<Plan?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        context.Plans.SingleOrDefaultAsync(plan => plan.Key == key, cancellationToken);

    public Task<TenantPlan?> GetTenantPlanAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.TenantPlans.SingleOrDefaultAsync(tenantPlan => tenantPlan.TenantId == tenantId, cancellationToken);

    public void Add(Plan plan) => context.Plans.Add(plan);

    public void Remove(Plan plan) => context.Plans.Remove(plan);

    public void Add(TenantPlan tenantPlan) => context.TenantPlans.Add(tenantPlan);

    public void Remove(TenantPlan tenantPlan) => context.TenantPlans.Remove(tenantPlan);
}
