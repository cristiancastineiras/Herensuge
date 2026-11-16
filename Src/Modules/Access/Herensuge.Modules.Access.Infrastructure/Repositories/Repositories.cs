using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Access.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Access.Infrastructure.Repositories;

internal sealed class ResourceServerRepository(AccessDbContext context) : IResourceServerRepository
{
    // Permissions and role templates are always loaded: split queries avoid a cartesian product.
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
    public async Task<IReadOnlyList<RoleAssignment>> ListAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await context.RoleAssignments.Where(assignment => assignment.TenantId == tenantId && assignment.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoleAssignment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.RoleAssignments.Where(assignment => assignment.UserId == userId).ToListAsync(cancellationToken);

    public void Add(RoleAssignment assignment) => context.RoleAssignments.Add(assignment);

    public void Remove(RoleAssignment assignment) => context.RoleAssignments.Remove(assignment);
}
