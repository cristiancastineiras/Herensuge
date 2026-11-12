using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Tokens.Events;

/// <summary>Carries the plain value so the notification can be sent. It only lives in the outbox (purged).</summary>
public sealed record OneTimeTokenIssuedDomainEvent(
    Guid TokenId,
    Guid UserId,
    OneTimeTokenPurpose Purpose,
    string? Target,
    string Value,
    DateTime ExpiresAtUtc) : DomainEvent;
