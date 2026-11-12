namespace Herensuge.Modules.Identity.Application.Mfa.StartPhoneVerification;

/// <summary>Sends a 6-digit code to the new number. The number is stored only once it is confirmed.</summary>
public sealed record StartPhoneVerificationCommand(Guid UserId, string PhoneNumber) : ICommand;
