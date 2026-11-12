namespace Herensuge.Modules.Identity.Application.Sessions.RevokeSession;

public sealed record RevokeSessionCommand(Guid UserId, Guid SessionId) : ICommand;
