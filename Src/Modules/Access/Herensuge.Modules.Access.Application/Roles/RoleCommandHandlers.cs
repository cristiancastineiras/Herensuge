using Herensuge.Modules.Access.Application.Services;

namespace Herensuge.Modules.Access.Application.Roles;

internal sealed class CreateRoleCommandHandler(
    TenantAdministration administration,
    IRoleRepository roles,
    IResourceServerRepository servers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequirePermissionAsync(command.TenantId, command.ActorUserId, TenantAdministration.ManageRoles, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        IReadOnlyList<Role> existing = await roles.ListAsync(command.TenantId, cancellationToken);
        if (existing.Count >= Role.MaxPerTenant)
        {
            return AccessErrors.TooManyRoles;
        }

        Result checks = await RoleRules.CheckAsync(existing, null, command.Name, command.PermissionIds, servers, cancellationToken);
        if (checks.IsFailure)
        {
            return checks.Error;
        }

        Result canGrant = await administration.EnsureCanGrantAsync(command.TenantId, command.ActorUserId, command.PermissionIds, [], cancellationToken);
        if (canGrant.IsFailure)
        {
            return canGrant.Error;
        }

        Result<Role> role = Role.Create(command.TenantId, command.Name, command.Description, command.PermissionIds, timeProvider.GetUtcNow().UtcDateTime);
        if (role.IsFailure)
        {
            return role.Error;
        }

        roles.Add(role.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return role.Value.Id;
    }
}

internal sealed class UpdateRoleCommandHandler(
    TenantAdministration administration,
    IRoleRepository roles,
    IResourceServerRepository servers,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateRoleCommand>
{
    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequirePermissionAsync(command.TenantId, command.ActorUserId, TenantAdministration.ManageRoles, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Role? role = await roles.GetAsync(command.TenantId, command.RoleId, cancellationToken);
        if (role is null)
        {
            return AccessErrors.RoleNotFound;
        }

        IReadOnlyList<Role> existing = await roles.ListAsync(command.TenantId, cancellationToken);
        Result checks = await RoleRules.CheckAsync(existing, role.Id, command.Name, command.PermissionIds, servers, cancellationToken);
        if (checks.IsFailure)
        {
            return checks;
        }

        // Only what is added must be held by the actor; removing permissions needs nothing (plan D12).
        Result canGrant = await administration.EnsureCanGrantAsync(
            command.TenantId, command.ActorUserId, command.PermissionIds.Except(role.PermissionIds), [], cancellationToken);
        if (canGrant.IsFailure)
        {
            return canGrant;
        }

        Result updated = role.Update(command.Name, command.Description, command.PermissionIds);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DeleteRoleCommandHandler(
    TenantAdministration administration,
    IRoleRepository roles,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteRoleCommand>
{
    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequirePermissionAsync(command.TenantId, command.ActorUserId, TenantAdministration.ManageRoles, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Role? role = await roles.GetAsync(command.TenantId, command.RoleId, cancellationToken);
        if (role is null)
        {
            return AccessErrors.RoleNotFound;
        }

        role.Delete();
        roles.Remove(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class RoleRules
{
    /// <summary>Unique name in the organization (case-insensitive) and only active permissions.</summary>
    public static async Task<Result> CheckAsync(
        IReadOnlyList<Role> existing,
        Guid? roleId,
        string name,
        IReadOnlyList<Guid> permissionIds,
        IResourceServerRepository servers,
        CancellationToken cancellationToken)
    {
        if (existing.Any(role => role.Id != roleId && string.Equals(role.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return AccessErrors.RoleNameInUse;
        }

        HashSet<Guid> active = [.. (await servers.ListAsync(cancellationToken)).SelectMany(server => server.ActivePermissions).Select(permission => permission.Id)];
        return permissionIds.All(active.Contains) ? Result.Success() : AccessErrors.UnknownPermission;
    }
}
