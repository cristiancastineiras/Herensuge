namespace Herensuge.Modules.Identity.Application.Passkeys.RenamePasskey;

public sealed record RenamePasskeyCommand(Guid UserId, Guid PasskeyId, string Name) : ICommand;
