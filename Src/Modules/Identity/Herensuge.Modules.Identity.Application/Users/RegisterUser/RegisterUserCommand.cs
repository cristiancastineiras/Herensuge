namespace Herensuge.Modules.Identity.Application.Users.RegisterUser;

/// <param name="ClientId">Application whose login page the user registers from: it may close registration or ask for more data.</param>
/// <param name="ProfileFields">Values of the application's extra registration fields.</param>
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    string? ClientId = null,
    IReadOnlyDictionary<string, string?>? ProfileFields = null) : ICommand;
