namespace Herensuge.Modules.Identity.Application.Sessions.ValidateSession;

/// <summary>Checks the SSO session is still active and slides its expiration (cookie validation, token refresh).</summary>
public sealed record ValidateSessionCommand(Guid SessionId, Guid UserId) : ICommand;
