using Herensuge.Common.Application.Messaging;

namespace Herensuge.Modules.Access.Contracts.IntegrationEvents;

/// <summary>An application registered or updated its manifest. Oidc creates its scope ("facturas.api" → "facturas-api").</summary>
public sealed record ResourceServerSyncedIntegrationEvent(
    Guid ResourceServerId,
    string Key,
    string Scope,
    string Audience,
    string ClientId,
    int ManifestVersion) : IntegrationEvent;

public sealed record RoleCreatedIntegrationEvent(Guid TenantId, Guid RoleId, string Name, IReadOnlyList<Guid> PermissionIds)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record RoleUpdatedIntegrationEvent(Guid TenantId, Guid RoleId, string Name, IReadOnlyList<Guid> PermissionIds)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record RoleDeletedIntegrationEvent(Guid TenantId, Guid RoleId, string Name)
    : IntegrationEvent, ITenantIntegrationEvent;

/// <param name="UserId">The member, when the subject is one (audit subject).</param>
/// <param name="SubjectType">user, group or client.</param>
public sealed record RoleAssignedIntegrationEvent(
    Guid TenantId,
    Guid? UserId,
    string SubjectType,
    Guid SubjectId,
    Guid? RoleId,
    Guid? RoleTemplateId,
    DateTime? ExpiresAtUtc) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record RoleUnassignedIntegrationEvent(
    Guid TenantId,
    Guid? UserId,
    string SubjectType,
    Guid SubjectId,
    Guid? RoleId,
    Guid? RoleTemplateId) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record GroupCreatedIntegrationEvent(Guid TenantId, Guid GroupId, string Name) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record GroupUpdatedIntegrationEvent(Guid TenantId, Guid GroupId, string Name) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record GroupDeletedIntegrationEvent(Guid TenantId, Guid GroupId, string Name) : IntegrationEvent, ITenantIntegrationEvent;

public sealed record GroupMembersChangedIntegrationEvent(Guid TenantId, Guid GroupId, IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record ServiceAccountCreatedIntegrationEvent(Guid TenantId, Guid ServiceAccountId, string Name, string ClientId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record ServiceAccountSecretRotatedIntegrationEvent(Guid TenantId, Guid ServiceAccountId, string ClientId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record ServiceAccountDeletedIntegrationEvent(Guid TenantId, Guid ServiceAccountId, string Name, string ClientId)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record PlanChangedIntegrationEvent(string Key, string Name, IReadOnlyList<string> Features, bool Deleted) : IntegrationEvent;

/// <param name="PlanKey">Null: the organization has the default plan.</param>
public sealed record TenantPlanChangedIntegrationEvent(Guid TenantId, string? PlanKey) : IntegrationEvent, ITenantIntegrationEvent;
