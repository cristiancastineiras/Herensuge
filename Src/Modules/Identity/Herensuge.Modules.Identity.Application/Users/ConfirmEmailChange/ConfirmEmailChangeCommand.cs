namespace Herensuge.Modules.Identity.Application.Users.ConfirmEmailChange;

public sealed record ConfirmEmailChangeCommand(string Token) : ICommand;
