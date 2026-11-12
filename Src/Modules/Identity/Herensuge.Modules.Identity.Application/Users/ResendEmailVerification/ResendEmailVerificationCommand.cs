namespace Herensuge.Modules.Identity.Application.Users.ResendEmailVerification;

public sealed record ResendEmailVerificationCommand(string Email) : ICommand;
