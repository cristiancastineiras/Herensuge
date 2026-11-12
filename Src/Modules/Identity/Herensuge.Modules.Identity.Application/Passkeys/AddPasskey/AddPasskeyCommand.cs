namespace Herensuge.Modules.Identity.Application.Passkeys.AddPasskey;

public sealed record AddPasskeyCommand(Guid UserId, string AttestationResponseJson, string Name) : ICommand<Guid>;
