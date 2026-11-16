using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.Roles.Events;

public sealed record RoleCreatedDomainEvent(Guid TenantId, Guid RoleId, string Name, IReadOnlyList<Guid> PermissionIds) : DomainEvent;

public sealed record RoleUpdatedDomainEvent(Guid TenantId, Guid RoleId, string Name, IReadOnlyList<Guid> PermissionIds) : DomainEvent;

public sealed record RoleDeletedDomainEvent(Guid TenantId, Guid RoleId, string Name) : DomainEvent;

public sealed record RoleAssignedDomainEvent(
    Guid TenantId,
    string SubjectType,
    Guid SubjectId,
    Guid? RoleId,
    Guid? RoleTemplateId,
    DateTime? ExpiresAtUtc) : DomainEvent;

public sealed record RoleUnassignedDomainEvent(Guid TenantId, string SubjectType, Guid SubjectId, Guid? RoleId, Guid? RoleTemplateId) : DomainEvent;
