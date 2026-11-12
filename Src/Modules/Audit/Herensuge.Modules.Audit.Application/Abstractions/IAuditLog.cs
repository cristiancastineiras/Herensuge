namespace Herensuge.Modules.Audit.Application.Abstractions;

/// <summary>Append-only audit log. Recording the same event twice has no effect.</summary>
public interface IAuditLog
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken);
}

/// <param name="SubjectUserId">User the event is about.</param>
/// <param name="ActorUserId">User who caused it (from the original request).</param>
/// <param name="Metadata">Event payload as JSON, without sensitive properties.</param>
public sealed record AuditRecord(
    Guid EventId,
    DateTime OccurredAtUtc,
    string Action,
    Guid? TenantId,
    Guid? SubjectUserId,
    Guid? ActorUserId,
    string? IpAddress,
    string? UserAgent,
    string Metadata);
