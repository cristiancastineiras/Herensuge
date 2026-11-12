namespace Herensuge.Modules.Identity.Application.Passkeys.CreatePasskeyRegistrationOptions;

/// <summary>Returns the WebAuthn creation options (JSON) for a new passkey.</summary>
public sealed record CreatePasskeyRegistrationOptionsCommand(Guid UserId) : ICommand<string>;
