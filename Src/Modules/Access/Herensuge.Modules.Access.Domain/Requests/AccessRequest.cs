using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.Requests;

public static class AccessRequestStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";

    /// <summary>Pending for more than a day: computed when read, there is no job (plan D13).</summary>
    public const string Expired = "expired";
}

/// <summary>
/// A member asks for a role (of the organization or of an application) for 1 to 72 hours, with a reason (plan D13).
/// Someone with access-requests:approve who holds its permissions resolves it; approving grants it until it expires.
/// </summary>
public sealed class AccessRequest : AggregateRoot
{
    public const int MinHours = 1;
    public const int MaxHours = 72;
    public const int MaxReasonLength = 500;
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromHours(24);

    private AccessRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? RoleId { get; private set; }

    public Guid? RoleTemplateId { get; private set; }

    public int Hours { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>pending, approved or denied; <see cref="StatusAt"/> adds expired.</summary>
    public string Status { get; private set; } = AccessRequestStatuses.Pending;

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public static Result<AccessRequest> Create(
        Guid tenantId,
        Guid userId,
        Guid? roleId,
        Guid? roleTemplateId,
        int hours,
        string reason,
        DateTime utcNow)
    {
        string trimmed = reason?.Trim() ?? string.Empty;
        if ((roleId is null) == (roleTemplateId is null) || hours is < MinHours or > MaxHours || trimmed.Length is 0 or > MaxReasonLength)
        {
            return AccessErrors.InvalidAccessRequest;
        }

        var request = new AccessRequest
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            RoleTemplateId = roleTemplateId,
            Hours = hours,
            Reason = trimmed,
            CreatedAtUtc = utcNow,
        };
        request.Raise(new AccessRequestedDomainEvent(tenantId, request.Id, userId, roleId, roleTemplateId, hours, trimmed));
        return request;
    }

    public string StatusAt(DateTime utcNow) =>
        Status == AccessRequestStatuses.Pending && utcNow - CreatedAtUtc > PendingLifetime ? AccessRequestStatuses.Expired : Status;

    /// <summary>The caller checks the approver's permission and the subset rule, and creates the assignment.</summary>
    public Result<DateTime> Approve(Guid approverUserId, DateTime utcNow)
    {
        Result decidable = EnsureDecidable(approverUserId, utcNow);
        if (decidable.IsFailure)
        {
            return decidable.Error;
        }

        Decide(AccessRequestStatuses.Approved, approverUserId, utcNow);
        DateTime expiresAt = utcNow.AddHours(Hours);
        Raise(new AccessRequestDecidedDomainEvent(TenantId, Id, UserId, Approved: true, approverUserId, expiresAt));
        return expiresAt;
    }

    public Result Deny(Guid approverUserId, DateTime utcNow)
    {
        Result decidable = EnsureDecidable(approverUserId, utcNow);
        if (decidable.IsFailure)
        {
            return decidable;
        }

        Decide(AccessRequestStatuses.Denied, approverUserId, utcNow);
        Raise(new AccessRequestDecidedDomainEvent(TenantId, Id, UserId, Approved: false, approverUserId, ExpiresAtUtc: null));
        return Result.Success();
    }

    private Result EnsureDecidable(Guid approverUserId, DateTime utcNow)
    {
        if (StatusAt(utcNow) != AccessRequestStatuses.Pending)
        {
            return AccessErrors.AccessRequestNotPending;
        }

        return approverUserId == UserId ? AccessErrors.CannotDecideOwnRequest : Result.Success();
    }

    private void Decide(string status, Guid approverUserId, DateTime utcNow)
    {
        Status = status;
        DecidedByUserId = approverUserId;
        DecidedAtUtc = utcNow;
    }
}

public sealed record AccessRequestedDomainEvent(
    Guid TenantId,
    Guid AccessRequestId,
    Guid UserId,
    Guid? RoleId,
    Guid? RoleTemplateId,
    int Hours,
    string Reason) : DomainEvent;

public sealed record AccessRequestDecidedDomainEvent(
    Guid TenantId,
    Guid AccessRequestId,
    Guid UserId,
    bool Approved,
    Guid DecidedByUserId,
    DateTime? ExpiresAtUtc) : DomainEvent;
