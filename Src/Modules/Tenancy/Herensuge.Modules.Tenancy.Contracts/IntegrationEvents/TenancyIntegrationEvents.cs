using Herensuge.Common.Application.Messaging;

namespace Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;

public sealed record TenantCreatedIntegrationEvent(Guid TenantId, string Name, string Slug, Guid CreatedByUserId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record TenantUpdatedIntegrationEvent(Guid TenantId, string Name, bool RequireMfa)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record TenantStatusChangedIntegrationEvent(Guid TenantId, bool IsActive)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record MemberAddedIntegrationEvent(Guid TenantId, Guid UserId, string Role)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record MemberRoleChangedIntegrationEvent(Guid TenantId, Guid UserId, string PreviousRole, string NewRole)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record MemberRemovedIntegrationEvent(Guid TenantId, Guid UserId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record InvitationCreatedIntegrationEvent(
    Guid TenantId,
    Guid InvitationId,
    string TenantName,
    string Email,
    string Role,
    Guid InvitedByUserId,
    [property: SensitiveData] string Token,
    DateTime ExpiresAtUtc) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record InvitationAcceptedIntegrationEvent(Guid TenantId, Guid InvitationId, Guid UserId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record InvitationRevokedIntegrationEvent(Guid TenantId, Guid InvitationId)
    : IntegrationEvent, ITenantIntegrationEvent;
