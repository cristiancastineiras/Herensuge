using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Access.Domain.ServiceAccounts;

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
    /// <summary>
    /// Assignments of any of <paramref name="subjectIds"/> (a user and their groups, a group, a service account).
    /// Expired ones included: callers filter with <see cref="RoleAssignment.IsActive"/>.
    /// </summary>
    Task<IReadOnlyList<RoleAssignment>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken);

    /// <summary>Every assignment of the user, in every organization (account deletion).</summary>
    Task<IReadOnlyList<RoleAssignment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(RoleAssignment assignment);

    void Remove(RoleAssignment assignment);
}

public interface IGroupRepository
{
    Task<Group?> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Group>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Groups of the organization the user belongs to.</summary>
    Task<IReadOnlyList<Group>> ListByMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Groups of every organization the user belongs to (account deletion).</summary>
    Task<IReadOnlyList<Group>> ListByMemberAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Group group);

    void Remove(Group group);
}

public interface IServiceAccountRepository
{
    Task<ServiceAccount?> GetAsync(Guid tenantId, Guid serviceAccountId, CancellationToken cancellationToken);

    Task<ServiceAccount?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);

    Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken);

    void Add(ServiceAccount account);

    void Remove(ServiceAccount account);
}

public interface IAccessRequestRepository
{
    Task<Requests.AccessRequest?> GetAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);

    void Add(Requests.AccessRequest request);
}

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> ListAsync(CancellationToken cancellationToken);

    Task<Plan?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    Task<TenantPlan?> GetTenantPlanAsync(Guid tenantId, CancellationToken cancellationToken);

    void Add(Plan plan);

    void Remove(Plan plan);

    void Add(TenantPlan tenantPlan);

    void Remove(TenantPlan tenantPlan);
}
