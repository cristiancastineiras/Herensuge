namespace Herensuge.Modules.Identity.Application.Mfa.DisableTotp;

public sealed record DisableTotpCommand(Guid UserId) : ICommand;
