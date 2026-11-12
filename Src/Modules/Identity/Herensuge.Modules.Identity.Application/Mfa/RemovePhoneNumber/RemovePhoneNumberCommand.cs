namespace Herensuge.Modules.Identity.Application.Mfa.RemovePhoneNumber;

public sealed record RemovePhoneNumberCommand(Guid UserId) : ICommand;
