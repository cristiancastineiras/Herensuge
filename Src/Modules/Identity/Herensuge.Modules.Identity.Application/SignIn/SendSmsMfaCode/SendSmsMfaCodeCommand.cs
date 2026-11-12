namespace Herensuge.Modules.Identity.Application.SignIn.SendSmsMfaCode;

public sealed record SendSmsMfaCodeCommand(Guid UserId) : ICommand;
