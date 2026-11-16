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

public sealed record RoleAssignedIntegrationEvent(Guid TenantId, Guid UserId, Guid? RoleId, Guid? RoleTemplateId, DateTime? ExpiresAtUtc)
    : IntegrationEvent, ITenantIntegrationEvent;

public sealed record RoleUnassignedIntegrationEvent(Guid TenantId, Guid UserId, Guid? RoleId, Guid? RoleTemplateId)
    : IntegrationEvent, ITenantIntegrationEvent;
