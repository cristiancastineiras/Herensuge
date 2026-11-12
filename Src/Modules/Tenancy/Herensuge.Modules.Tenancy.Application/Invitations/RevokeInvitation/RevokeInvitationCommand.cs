namespace Herensuge.Modules.Tenancy.Application.Invitations.RevokeInvitation;

public sealed record RevokeInvitationCommand(Guid TenantId, Guid ActorUserId, Guid InvitationId) : ICommand;
