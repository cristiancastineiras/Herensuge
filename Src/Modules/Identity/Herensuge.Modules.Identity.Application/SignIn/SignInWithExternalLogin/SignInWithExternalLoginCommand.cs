namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithExternalLogin;

/// <param name="Provider">google or microsoft.</param>
/// <param name="Subject">Stable identifier at the provider (never the email).</param>
/// <param name="EmailVerified">Only trusted when the provider guarantees it (Google).</param>
/// <param name="LinkToUserId">Set when an already signed-in user links the provider to their account.</param>
public sealed record SignInWithExternalLoginCommand(
    string Provider,
    string Subject,
    string? Email,
    bool EmailVerified,
    string? DisplayName,
    Guid? LinkToUserId) : ICommand<SignInResult>;
