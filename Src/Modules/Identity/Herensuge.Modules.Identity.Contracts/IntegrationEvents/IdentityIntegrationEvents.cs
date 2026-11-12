using Herensuge.Common.Application.Messaging;

namespace Herensuge.Modules.Identity.Contracts.IntegrationEvents;

public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email, string DisplayName, string Method) : IntegrationEvent;

public sealed record EmailVerificationRequestedIntegrationEvent(
    Guid UserId,
    string Email,
    string DisplayName,
    [property: SensitiveData] string Token,
    DateTime ExpiresAtUtc) : IntegrationEvent;

public sealed record EmailVerifiedIntegrationEvent(Guid UserId, string Email) : IntegrationEvent;

public sealed record EmailChangeRequestedIntegrationEvent(
    Guid UserId,
    string CurrentEmail,
    string NewEmail,
    string DisplayName,
    [property: SensitiveData] string Token,
    DateTime ExpiresAtUtc) : IntegrationEvent;

public sealed record EmailChangedIntegrationEvent(Guid UserId, string PreviousEmail, string NewEmail) : IntegrationEvent;

public sealed record PasswordResetRequestedIntegrationEvent(
    Guid UserId,
    string Email,
    string DisplayName,
    [property: SensitiveData] string Token,
    DateTime ExpiresAtUtc) : IntegrationEvent;

public sealed record PasswordChangedIntegrationEvent(Guid UserId) : IntegrationEvent;

public sealed record SignInSucceededIntegrationEvent(Guid UserId, Guid SessionId, IReadOnlyList<string> AuthenticationMethods) : IntegrationEvent;

public sealed record SignInFailedIntegrationEvent(Guid UserId, string Reason) : IntegrationEvent;

public sealed record UserLockedOutIntegrationEvent(Guid UserId, DateTime LockedUntilUtc) : IntegrationEvent;

public sealed record NewDeviceSignedInIntegrationEvent(Guid UserId, string? IpAddress, string? UserAgent) : IntegrationEvent;

public sealed record SessionRevokedIntegrationEvent(Guid UserId, Guid SessionId, string Reason) : IntegrationEvent;

public sealed record MfaEnabledIntegrationEvent(Guid UserId, string Method) : IntegrationEvent;

public sealed record MfaDisabledIntegrationEvent(Guid UserId, string Method) : IntegrationEvent;

public sealed record PhoneVerificationRequestedIntegrationEvent(
    Guid UserId,
    [property: SensitiveData] string PhoneNumber,
    [property: SensitiveData] string Code,
    DateTime ExpiresAtUtc) : IntegrationEvent;

public sealed record SmsMfaCodeRequestedIntegrationEvent(
    Guid UserId,
    [property: SensitiveData] string PhoneNumber,
    [property: SensitiveData] string Code,
    DateTime ExpiresAtUtc) : IntegrationEvent;

public sealed record PasskeyAddedIntegrationEvent(Guid UserId, Guid PasskeyId, string Name) : IntegrationEvent;

public sealed record PasskeyRemovedIntegrationEvent(Guid UserId, Guid PasskeyId) : IntegrationEvent;

public sealed record ExternalLoginLinkedIntegrationEvent(Guid UserId, string Provider) : IntegrationEvent;

public sealed record ExternalLoginUnlinkedIntegrationEvent(Guid UserId, string Provider) : IntegrationEvent;

public sealed record PlatformAdminGrantedIntegrationEvent(Guid UserId) : IntegrationEvent;

public sealed record UserDeletedIntegrationEvent(Guid UserId) : IntegrationEvent;
