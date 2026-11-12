namespace Herensuge.Modules.Identity.Application.Users.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
