namespace Herensuge.Modules.Oidc.Application.Authorization.EndSession;

/// <summary>OIDC logout (/connect/logout): ends the SSO session behind the cookie.</summary>
public sealed record EndSessionCommand(Guid SessionId) : ICommand;
