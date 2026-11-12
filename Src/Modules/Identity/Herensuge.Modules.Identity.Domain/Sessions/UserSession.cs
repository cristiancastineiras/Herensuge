using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Domain.Sessions.Events;

namespace Herensuge.Modules.Identity.Domain.Sessions;

/// <summary>
/// Single sign-on session at Herensuge. The IdP cookie, the OIDC authorizations and the refresh
/// tokens issued from it all die when it is revoked.
/// </summary>
public sealed class UserSession : AggregateRoot
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(14);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);

    private static readonly TimeSpan TouchThreshold = TimeSpan.FromMinutes(1);

    private UserSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string[] AuthenticationMethods { get; private set; } = [];

    public DateTime AuthenticatedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? DeviceIdHash { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? RevocationReason { get; private set; }

    public static UserSession Start(
        Guid userId,
        IEnumerable<string> authenticationMethods,
        string? ipAddress,
        string? userAgent,
        string? deviceIdHash,
        DateTime utcNow)
    {
        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AuthenticationMethods = [.. authenticationMethods.Distinct(StringComparer.Ordinal)],
            AuthenticatedAtUtc = utcNow,
            CreatedAtUtc = utcNow,
            LastSeenAtUtc = utcNow,
            ExpiresAtUtc = utcNow + IdleTimeout,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceIdHash = deviceIdHash,
        };

        session.Raise(new SessionStartedDomainEvent(session.Id, userId, session.AuthenticationMethods));
        return session;
    }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;

    /// <summary>Slides the expiration (at most once a minute) without passing the absolute lifetime.</summary>
    public bool Touch(DateTime utcNow)
    {
        if (!IsActive(utcNow))
        {
            return false;
        }

        if (utcNow - LastSeenAtUtc >= TouchThreshold)
        {
            LastSeenAtUtc = utcNow;
            DateTime sliding = utcNow + IdleTimeout;
            DateTime absolute = CreatedAtUtc + AbsoluteLifetime;
            ExpiresAtUtc = sliding < absolute ? sliding : absolute;
        }

        return true;
    }

    /// <summary>Adds the factors verified in a step-up and refreshes the authentication time.</summary>
    public void StepUp(IEnumerable<string> authenticationMethods, DateTime utcNow)
    {
        AuthenticationMethods = [.. AuthenticationMethods.Union(authenticationMethods, StringComparer.Ordinal)];
        AuthenticatedAtUtc = utcNow;
    }

    public void Revoke(string reason, DateTime utcNow)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = utcNow;
        RevocationReason = reason;
        Raise(new SessionRevokedDomainEvent(Id, UserId, reason));
    }
}

public static class SessionRevocationReasons
{
    public const string SignOut = "sign_out";
    public const string RevokedByUser = "revoked_by_user";
    public const string PasswordChanged = "password_changed";
}

public static class SessionErrors
{
    public static readonly Error NotFound = Error.NotFound("Sessions.NotFound", "The session does not exist.");

    public static readonly Error Inactive = Error.Unauthorized("Sessions.Inactive", "The session has expired or was revoked.");
}
