namespace Herensuge.Modules.Identity.Application.Users.ChangePassword;

/// <param name="CurrentPassword">Required when the account already has a password.</param>
/// <param name="CurrentSessionId">Kept open; every other session is revoked.</param>
public sealed record ChangePasswordCommand(Guid UserId, string? CurrentPassword, string NewPassword, Guid? CurrentSessionId) : ICommand;
