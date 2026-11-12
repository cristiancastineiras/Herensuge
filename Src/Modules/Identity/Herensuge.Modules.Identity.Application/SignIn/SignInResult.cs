using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Users;

namespace Herensuge.Modules.Identity.Application.SignIn;

public enum SignInStatus
{
    SignedIn,
    MfaRequired,
    Linked,
}

/// <summary>Outcome of a sign-in step. The presentation layer turns it into cookies.</summary>
public sealed record SignInResult(
    SignInStatus Status,
    Guid UserId,
    Guid? SessionId,
    IReadOnlyList<string> Methods,
    DateTime? AuthenticatedAtUtc,
    IReadOnlyList<string> MfaMethods)
{
    public static SignInResult SignedIn(UserSession session) =>
        new(SignInStatus.SignedIn, session.UserId, session.Id, session.AuthenticationMethods, session.AuthenticatedAtUtc, []);

    public static SignInResult MfaRequired(User user, IReadOnlyList<string> firstFactorMethods) =>
        new(SignInStatus.MfaRequired, user.Id, null, firstFactorMethods, null, user.GetMfaMethods());

    public static SignInResult Linked(Guid userId) =>
        new(SignInStatus.Linked, userId, null, [], null, []);
}
