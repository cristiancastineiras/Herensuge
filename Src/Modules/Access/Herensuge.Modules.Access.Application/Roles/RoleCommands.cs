namespace Herensuge.Modules.Access.Application.Roles;

/// <summary>Organization role. <paramref name="PermissionIds"/> are active permissions of any application.</summary>
public sealed record CreateRoleCommand(Guid TenantId, Guid ActorUserId, string Name, string? Description, IReadOnlyList<Guid> PermissionIds)
    : ICommand<Guid>;

public sealed record UpdateRoleCommand(Guid TenantId, Guid ActorUserId, Guid RoleId, string Name, string? Description, IReadOnlyList<Guid> PermissionIds)
    : ICommand;

/// <summary>Also removes its assignments.</summary>
public sealed record DeleteRoleCommand(Guid TenantId, Guid ActorUserId, Guid RoleId) : ICommand;
