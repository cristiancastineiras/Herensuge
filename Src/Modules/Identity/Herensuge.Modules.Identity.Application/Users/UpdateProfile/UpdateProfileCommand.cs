namespace Herensuge.Modules.Identity.Application.Users.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, string DisplayName) : ICommand;
