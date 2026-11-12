using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Sessions.Events;
using Herensuge.Modules.Identity.Domain.Tokens;

namespace Herensuge.Modules.Identity.UnitTests;

public sealed class SessionAndTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Session_slides_but_never_passes_its_absolute_lifetime()
    {
        UserSession session = UserSession.Start(Guid.NewGuid(), ["pwd"], null, null, null, Now);

        DateTime later = Now + TimeSpan.FromDays(25);
        for (DateTime moment = Now; moment <= later; moment += TimeSpan.FromDays(10))
        {
            Assert.True(session.Touch(moment));
        }

        Assert.True(session.Touch(later));
        Assert.Equal(Now + UserSession.AbsoluteLifetime, session.ExpiresAtUtc);
        Assert.False(session.IsActive(Now + UserSession.AbsoluteLifetime));
    }

    [Fact]
    public void Idle_session_expires()
    {
        UserSession session = UserSession.Start(Guid.NewGuid(), ["pwd"], null, null, null, Now);

        Assert.False(session.Touch(Now + UserSession.IdleTimeout + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Revoking_is_idempotent_and_raises_one_event()
    {
        UserSession session = UserSession.Start(Guid.NewGuid(), ["pwd"], null, null, null, Now);

        session.Revoke(SessionRevocationReasons.SignOut, Now);
        session.Revoke(SessionRevocationReasons.SignOut, Now);

        Assert.False(session.IsActive(Now));
        Assert.Single(session.GetDomainEvents().OfType<SessionRevokedDomainEvent>());
    }

    [Fact]
    public void Link_tokens_are_single_use()
    {
        (OneTimeToken token, string value) = OneTimeToken.Issue(Guid.NewGuid(), OneTimeTokenPurpose.PasswordReset, "ana@example.com", Now);

        Assert.Equal(OneTimeToken.ComputeLinkHash(value), token.TokenHash);
        Assert.True(token.Consume(value, Now).IsSuccess);
        Assert.Equal(OneTimeTokenErrors.InvalidOrExpired, token.Consume(value, Now).Error);
    }

    [Fact]
    public void Tokens_expire()
    {
        (OneTimeToken token, string value) = OneTimeToken.Issue(Guid.NewGuid(), OneTimeTokenPurpose.PasswordReset, "ana@example.com", Now);

        Assert.Equal(OneTimeTokenErrors.InvalidOrExpired, token.Consume(value, Now + TimeSpan.FromMinutes(16)).Error);
    }

    [Fact]
    public void Numeric_codes_stop_working_after_five_wrong_attempts()
    {
        (OneTimeToken token, string value) = OneTimeToken.Issue(Guid.NewGuid(), OneTimeTokenPurpose.SmsMfa, "+34600000000", Now);
        Assert.Matches("^[0-9]{6}$", value);

        string wrong = value == "000000" ? "000001" : "000000";
        for (int attempt = 0; attempt < OneTimeToken.MaxFailedAttempts; attempt++)
        {
            Assert.True(token.Consume(wrong, Now).IsFailure);
        }

        Assert.Equal(OneTimeTokenErrors.InvalidOrExpired, token.Consume(value, Now).Error);
    }
}
