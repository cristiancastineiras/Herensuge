namespace Herensuge.Modules.Identity.Application.ExternalLogins.UnlinkExternalLogin;

public sealed record UnlinkExternalLoginCommand(Guid UserId, string Provider) : ICommand;
