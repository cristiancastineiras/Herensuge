namespace Herensuge.Modules.Identity.Application.Users.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;
