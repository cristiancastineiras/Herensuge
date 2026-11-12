using Herensuge.Common.Domain;

namespace Herensuge.Modules.Tenancy.Domain.Tenants.Events;

public sealed record TenantCreatedDomainEvent(Guid TenantId, string Name, string Slug, Guid CreatedByUserId) : DomainEvent;

public sealed record TenantUpdatedDomainEvent(Guid TenantId, string Name, bool RequireMfa) : DomainEvent;

public sealed record TenantStatusChangedDomainEvent(Guid TenantId, bool IsActive) : DomainEvent;
