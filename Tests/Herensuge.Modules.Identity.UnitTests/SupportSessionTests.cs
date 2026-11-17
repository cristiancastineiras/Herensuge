using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Sessions.Events;
using Herensuge.Modules.Identity.Domain.Tokens;

namespace Herensuge.Modules.Identity.UnitTests;

/// <summary>Read-only support sessions (plan D11).</summary>
public sealed class SupportSessionTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void A_support_session_is_imp_and_never_outlives_30_minutes()
    {
        UserSession session = UserSession.StartSupport(User, Admin, "203.0.113.9", "browser", Now);

        Assert.True(session.IsSupport);
        Assert.Equal(["imp"], session.AuthenticationMethods);
        Assert.Single(session.GetDomainEvents().OfType<SupportSessionStartedDomainEvent>());

        // Activity slides normal sessions; this one stays capped.
        Assert.True(session.Touch(Now.AddMinutes(20)));
        Assert.Equal(Now.AddMinutes(30), session.ExpiresAtUtc);
        Assert.False(session.IsActive(Now.AddMinutes(30)));
    }

    [Fact]
    public void Ending_it_tells_who_was_acting()
    {
        UserSession session = UserSession.StartSupport(User, Admin, null, null, Now);

        session.Revoke(SessionRevocationReasons.SupportExpired, Now.AddMinutes(31));

        SupportSessionEndedDomainEvent ended = Assert.Single(session.GetDomainEvents().OfType<SupportSessionEndedDomainEvent>());
        Assert.Equal(Admin, ended.ImpersonatorUserId);
        Assert.Equal(SessionRevocationReasons.SupportExpired, ended.Reason);
    }

    [Fact]
    public void A_users_own_session_has_no_actor()
    {
        UserSession session = UserSession.Start(User, ["pwd"], null, null, null, Now);
        session.Revoke(SessionRevocationReasons.SignOut, Now);

        Assert.False(session.IsSupport);
        Assert.Empty(session.GetDomainEvents().OfType<SupportSessionEndedDomainEvent>());
    }

    [Fact]
    public void The_support_link_lasts_five_minutes_and_names_the_administrator()
    {
        (OneTimeToken token, _) = OneTimeToken.Issue(User, OneTimeTokenPurpose.SupportSession, Admin.ToString(), TestKeys.Hasher, Now);

        Assert.Equal(Now.AddMinutes(5), token.ExpiresAtUtc);
        Assert.Equal(Admin.ToString(), token.Target);
    }
}
