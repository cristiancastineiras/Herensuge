namespace Herensuge.Modules.Identity.Application.Users.GetMfaChallenge;

/// <summary>Second factors the user can answer with during sign-in.</summary>
public sealed record GetMfaChallengeQuery(Guid UserId) : IQuery<MfaChallengeResponse>;

public sealed record MfaChallengeResponse(IReadOnlyList<string> Methods, string? PhoneNumberHint);
