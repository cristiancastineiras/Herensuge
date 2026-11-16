using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.ResourceServers.Events;

public sealed record ResourceServerSyncedDomainEvent(
    Guid ResourceServerId,
    string Key,
    string Scope,
    string Audience,
    string ClientId,
    int ManifestVersion) : DomainEvent;
