namespace Herensuge.Modules.Identity.Application.Mfa.BeginTotpSetup;

public sealed record BeginTotpSetupCommand(Guid UserId) : ICommand<TotpSetupResponse>;

public sealed record TotpSetupResponse(string SharedKey, string AuthenticatorUri);
