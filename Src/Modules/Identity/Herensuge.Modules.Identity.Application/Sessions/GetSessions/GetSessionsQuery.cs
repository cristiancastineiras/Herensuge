namespace Herensuge.Modules.Identity.Application.Sessions.GetSessions;

public sealed record GetSessionsQuery(Guid UserId, Guid? CurrentSessionId) : IQuery<IReadOnlyList<SessionResponse>>;

public sealed record SessionResponse
{
    public Guid Id { get; init; }

    public string[] AuthenticationMethods { get; init; } = [];

    public DateTime AuthenticatedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime LastSeenAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public bool IsCurrent { get; init; }
}
