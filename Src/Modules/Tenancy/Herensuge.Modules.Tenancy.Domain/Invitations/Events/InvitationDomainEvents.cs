using Herensuge.Common.Domain;
using Herensuge.Modules.Tenancy.Domain.Memberships;

namespace Herensuge.Modules.Tenancy.Domain.Invitations.Events;

/// <summary>Carries the plain token so the email can be sent. It only lives in the outbox (purged).</summary>
public sealed record InvitationCreatedDomainEvent(
    Guid InvitationId,
    Guid TenantId,
    string TenantName,
    string Email,
    TenantRole Role,
    Guid InvitedByUserId,
    string Token,
    DateTime ExpiresAtUtc) : DomainEvent;

public sealed record InvitationAcceptedDomainEvent(Guid InvitationId, Guid TenantId, Guid UserId) : DomainEvent;

public sealed record InvitationRevokedDomainEvent(Guid InvitationId, Guid TenantId) : DomainEvent;
