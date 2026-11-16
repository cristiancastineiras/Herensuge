namespace Herensuge.Modules.Identity.Application.SignIn.SendMfaCode;

/// <summary>Sends a 6-digit sign-in code by SMS or to the verified email.</summary>
/// <param name="Method">sms or email.</param>
public sealed record SendMfaCodeCommand(Guid UserId, string Method) : ICommand;
