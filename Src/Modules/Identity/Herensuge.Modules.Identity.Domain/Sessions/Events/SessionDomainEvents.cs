using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Sessions.Events;

public sealed record SessionStartedDomainEvent(Guid SessionId, Guid UserId, IReadOnlyList<string> AuthenticationMethods) : DomainEvent;

public sealed record SessionRevokedDomainEvent(Guid SessionId, Guid UserId, string Reason) : DomainEvent;
