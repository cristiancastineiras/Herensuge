namespace Herensuge.Modules.Identity.Application.Users.DeleteAccount;

public sealed record DeleteAccountCommand(Guid UserId) : ICommand;
