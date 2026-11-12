namespace Herensuge.Modules.Identity.Application.Users.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : ICommand;
