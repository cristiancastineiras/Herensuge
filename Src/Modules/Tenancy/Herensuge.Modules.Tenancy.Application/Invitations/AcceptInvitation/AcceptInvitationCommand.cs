namespace Herensuge.Modules.Tenancy.Application.Invitations.AcceptInvitation;

/// <returns>The organization joined.</returns>
public sealed record AcceptInvitationCommand(string Token, Guid UserId) : ICommand<Guid>;
