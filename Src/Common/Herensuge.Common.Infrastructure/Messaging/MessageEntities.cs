namespace Herensuge.Common.Infrastructure.Messaging;

// EF maps these tables (one set per module schema). The jobs read and write them with raw SQL.
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    public string? CorrelationId { get; init; }

    public string? TraceParent { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

public sealed class OutboxMessageConsumer
{
    public Guid OutboxMessageId { get; init; }

    public required string Name { get; init; }
}

public sealed class InboxMessage
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    public string? CorrelationId { get; init; }

    public string? TraceParent { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

public sealed class InboxMessageConsumer
{
    public Guid InboxMessageId { get; init; }

    public required string Name { get; init; }
}
