using Herensuge.Common.Domain;

namespace Herensuge.Modules.Tenancy.Domain.Memberships.Events;

public sealed record MemberAddedDomainEvent(Guid TenantId, Guid UserId, TenantRole Role) : DomainEvent;

public sealed record MemberRoleChangedDomainEvent(Guid TenantId, Guid UserId, TenantRole PreviousRole, TenantRole NewRole) : DomainEvent;

public sealed record MemberRemovedDomainEvent(Guid TenantId, Guid UserId) : DomainEvent;
