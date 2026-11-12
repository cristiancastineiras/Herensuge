namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithPasskey;

public sealed record SignInWithPasskeyCommand(string AssertionResponseJson) : ICommand<SignInResult>;
