using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;

namespace Herensuge.Modules.Access.Domain;

public interface IResourceServerRepository
{
    Task<ResourceServer?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    Task<ResourceServer?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ResourceServer>> ListAsync(CancellationToken cancellationToken);

    void Add(ResourceServer server);
}

public interface IRoleRepository
{
    Task<Role?> GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetManyAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    void Add(Role role);

    void Remove(Role role);
}

public interface IRoleAssignmentRepository
{
    /// <summary>Expired ones included: callers filter with <see cref="RoleAssignment.IsActive"/>.</summary>
    Task<IReadOnlyList<RoleAssignment>> ListAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Every assignment of the user, in every organization (account deletion).</summary>
    Task<IReadOnlyList<RoleAssignment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(RoleAssignment assignment);

    void Remove(RoleAssignment assignment);
}
