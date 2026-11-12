namespace Herensuge.Modules.Identity.Application.Passkeys.RemovePasskey;

public sealed record RemovePasskeyCommand(Guid UserId, Guid PasskeyId) : ICommand;
