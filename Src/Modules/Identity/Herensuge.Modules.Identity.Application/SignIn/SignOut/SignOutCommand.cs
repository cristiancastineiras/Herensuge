namespace Herensuge.Modules.Identity.Application.SignIn.SignOut;

public sealed record SignOutCommand(Guid SessionId) : ICommand;
