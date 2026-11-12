namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithPassword;

public sealed record SignInWithPasswordCommand(string Email, string Password) : ICommand<SignInResult>;
