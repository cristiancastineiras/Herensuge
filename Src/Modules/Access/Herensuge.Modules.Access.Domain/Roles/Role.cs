using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Domain.Roles.Events;

namespace Herensuge.Modules.Access.Domain.Roles;

/// <summary>Role defined by an organization: any combination of the permissions of its applications.</summary>
public sealed class Role : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxPermissions = 500;
    public const int MaxPerTenant = 100;

    private Role()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Ids of <see cref="ResourceServers.Permission"/>, of any application.</summary>
    public Guid[] PermissionIds { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Role> Create(Guid tenantId, string name, string? description, IReadOnlyCollection<Guid> permissionIds, DateTime utcNow)
    {
        var role = new Role { Id = Guid.CreateVersion7(), TenantId = tenantId, CreatedAtUtc = utcNow };
        Result set = role.Set(name, description, permissionIds);
        if (set.IsFailure)
        {
            return set.Error;
        }

        role.Raise(new RoleCreatedDomainEvent(tenantId, role.Id, role.Name, role.PermissionIds));
        return role;
    }

    public Result Update(string name, string? description, IReadOnlyCollection<Guid> permissionIds)
    {
        Result set = Set(name, description, permissionIds);
        if (set.IsSuccess)
        {
            Raise(new RoleUpdatedDomainEvent(TenantId, Id, Name, PermissionIds));
        }

        return set;
    }

    /// <summary>Its assignments are removed with it (database cascade).</summary>
    public void Delete() => Raise(new RoleDeletedDomainEvent(TenantId, Id, Name));

    private Result Set(string name, string? description, IReadOnlyCollection<Guid> permissionIds)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            return AccessErrors.InvalidRoleName;
        }

        if (permissionIds.Count > MaxPermissions)
        {
            return AccessErrors.TooManyPermissions;
        }

        Name = trimmed;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PermissionIds = [.. permissionIds.Distinct()];
        return Result.Success();
    }
}
