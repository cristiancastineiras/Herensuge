namespace Herensuge.Modules.Identity.Application.Users.RegisterUser;

public sealed record RegisterUserCommand(string Email, string Password, string DisplayName) : ICommand;
