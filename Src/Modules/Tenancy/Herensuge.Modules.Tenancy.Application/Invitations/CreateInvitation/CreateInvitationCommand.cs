namespace Herensuge.Modules.Tenancy.Application.Invitations.CreateInvitation;

/// <param name="Role">owner, admin or member.</param>
public sealed record CreateInvitationCommand(Guid TenantId, Guid ActorUserId, string Email, string Role) : ICommand<Guid>;
