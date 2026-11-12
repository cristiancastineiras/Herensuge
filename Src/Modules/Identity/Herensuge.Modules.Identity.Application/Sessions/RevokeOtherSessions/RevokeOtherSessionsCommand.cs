namespace Herensuge.Modules.Identity.Application.Sessions.RevokeOtherSessions;

public sealed record RevokeOtherSessionsCommand(Guid UserId, Guid? CurrentSessionId) : ICommand;
