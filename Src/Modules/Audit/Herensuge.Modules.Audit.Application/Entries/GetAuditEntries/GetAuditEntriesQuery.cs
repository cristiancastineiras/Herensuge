using System.Text.Json;
using Herensuge.Common.Application.Messaging;

namespace Herensuge.Modules.Audit.Application.Entries.GetAuditEntries;

/// <summary>Newest first. Pass the NextCursor of a page as Before to get the following one.</summary>
public sealed record GetAuditEntriesQuery(
    Guid? TenantId,
    Guid? UserId,
    string? Action,
    DateTime? Before,
    int PageSize) : IQuery<AuditPage>;

public sealed record AuditPage(IReadOnlyList<AuditEntryResponse> Items, DateTime? NextCursor);

public sealed record AuditEntryResponse(
    Guid Id,
    DateTime OccurredAtUtc,
    string Action,
    Guid? TenantId,
    Guid? SubjectUserId,
    Guid? ActorUserId,
    string? IpAddress,
    string? UserAgent,
    JsonElement Metadata);
