namespace Herensuge.Modules.Identity.Application.Users.RequestEmailChange;

public sealed record RequestEmailChangeCommand(Guid UserId, string NewEmail) : ICommand;
