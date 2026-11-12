namespace Herensuge.Modules.Tenancy.Application.Members.ChangeMemberRole;

/// <param name="Role">owner, admin or member.</param>
public sealed record ChangeMemberRoleCommand(Guid TenantId, Guid ActorUserId, Guid UserId, string Role) : ICommand;
