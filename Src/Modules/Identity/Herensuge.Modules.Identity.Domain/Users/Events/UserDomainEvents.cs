using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Users.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email, string DisplayName, string Method) : DomainEvent;

public sealed record EmailVerifiedDomainEvent(Guid UserId, string Email) : DomainEvent;

public sealed record EmailChangedDomainEvent(Guid UserId, string PreviousEmail, string NewEmail) : DomainEvent;

/// <param name="KeepSessionId">Session that changed the password; every other session is revoked.</param>
/// <summary>ChangedAtUtc: sessions started after it (for example, the sign-in right after a reset) stay open.</summary>
public sealed record PasswordChangedDomainEvent(Guid UserId, Guid? KeepSessionId, DateTime ChangedAtUtc) : DomainEvent;

public sealed record SignInFailedDomainEvent(Guid UserId, string Reason) : DomainEvent;

public sealed record UserLockedOutDomainEvent(Guid UserId, DateTime LockedUntilUtc) : DomainEvent;

public sealed record NewDeviceSignedInDomainEvent(Guid UserId, string? IpAddress, string? UserAgent) : DomainEvent;

public sealed record MfaEnabledDomainEvent(Guid UserId, string Method) : DomainEvent;

public sealed record MfaDisabledDomainEvent(Guid UserId, string Method) : DomainEvent;

public sealed record PasskeyAddedDomainEvent(Guid UserId, Guid PasskeyId, string Name) : DomainEvent;

public sealed record PasskeyRemovedDomainEvent(Guid UserId, Guid PasskeyId) : DomainEvent;

public sealed record ExternalLoginLinkedDomainEvent(Guid UserId, string Provider) : DomainEvent;

public sealed record ExternalLoginUnlinkedDomainEvent(Guid UserId, string Provider) : DomainEvent;

public sealed record PlatformAdminGrantedDomainEvent(Guid UserId) : DomainEvent;

public sealed record UserDeletedDomainEvent(Guid UserId, string Email, string DisplayName) : DomainEvent;
