using Herensuge.Modules.Identity.Contracts.IntegrationEvents;
using Herensuge.Modules.Identity.Domain.Sessions.Events;
using Herensuge.Modules.Identity.Domain.Tokens.Events;
using Herensuge.Modules.Identity.Domain.Users.Events;

namespace Herensuge.Modules.Identity.Application.EventHandlers;

/// <summary>Translates Identity domain events into the integration events other modules listen to.</summary>
internal sealed class IdentityIntegrationEventPublisher(IEventBus eventBus, IUserRepository users) :
    IDomainEventHandler<UserRegisteredDomainEvent>,
    IDomainEventHandler<EmailVerifiedDomainEvent>,
    IDomainEventHandler<EmailChangedDomainEvent>,
    IDomainEventHandler<PasswordChangedDomainEvent>,
    IDomainEventHandler<SignInFailedDomainEvent>,
    IDomainEventHandler<UserLockedOutDomainEvent>,
    IDomainEventHandler<NewDeviceSignedInDomainEvent>,
    IDomainEventHandler<MfaEnabledDomainEvent>,
    IDomainEventHandler<MfaDisabledDomainEvent>,
    IDomainEventHandler<PasskeyAddedDomainEvent>,
    IDomainEventHandler<PasskeyRemovedDomainEvent>,
    IDomainEventHandler<ExternalLoginLinkedDomainEvent>,
    IDomainEventHandler<ExternalLoginUnlinkedDomainEvent>,
    IDomainEventHandler<PlatformAdminGrantedDomainEvent>,
    IDomainEventHandler<UserDeletedDomainEvent>,
    IDomainEventHandler<SessionStartedDomainEvent>,
    IDomainEventHandler<SessionRevokedDomainEvent>,
    IDomainEventHandler<OneTimeTokenIssuedDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new UserRegisteredIntegrationEvent(domainEvent.UserId, domainEvent.Email, domainEvent.DisplayName, domainEvent.Method),
            cancellationToken);

    public Task Handle(EmailVerifiedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new EmailVerifiedIntegrationEvent(domainEvent.UserId, domainEvent.Email), cancellationToken);

    public Task Handle(EmailChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new EmailChangedIntegrationEvent(domainEvent.UserId, domainEvent.PreviousEmail, domainEvent.NewEmail),
            cancellationToken);

    public Task Handle(PasswordChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new PasswordChangedIntegrationEvent(domainEvent.UserId), cancellationToken);

    public Task Handle(SignInFailedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new SignInFailedIntegrationEvent(domainEvent.UserId, domainEvent.Reason), cancellationToken);

    public Task Handle(UserLockedOutDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new UserLockedOutIntegrationEvent(domainEvent.UserId, domainEvent.LockedUntilUtc), cancellationToken);

    public Task Handle(NewDeviceSignedInDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new NewDeviceSignedInIntegrationEvent(domainEvent.UserId, domainEvent.IpAddress, domainEvent.UserAgent),
            cancellationToken);

    public Task Handle(MfaEnabledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new MfaEnabledIntegrationEvent(domainEvent.UserId, domainEvent.Method), cancellationToken);

    public Task Handle(MfaDisabledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new MfaDisabledIntegrationEvent(domainEvent.UserId, domainEvent.Method), cancellationToken);

    public Task Handle(PasskeyAddedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new PasskeyAddedIntegrationEvent(domainEvent.UserId, domainEvent.PasskeyId, domainEvent.Name),
            cancellationToken);

    public Task Handle(PasskeyRemovedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new PasskeyRemovedIntegrationEvent(domainEvent.UserId, domainEvent.PasskeyId), cancellationToken);

    public Task Handle(ExternalLoginLinkedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new ExternalLoginLinkedIntegrationEvent(domainEvent.UserId, domainEvent.Provider), cancellationToken);

    public Task Handle(ExternalLoginUnlinkedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new ExternalLoginUnlinkedIntegrationEvent(domainEvent.UserId, domainEvent.Provider), cancellationToken);

    public Task Handle(PlatformAdminGrantedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new PlatformAdminGrantedIntegrationEvent(domainEvent.UserId), cancellationToken);

    public Task Handle(UserDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new UserDeletedIntegrationEvent(domainEvent.UserId), cancellationToken);

    public Task Handle(SessionStartedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new SignInSucceededIntegrationEvent(domainEvent.UserId, domainEvent.SessionId, domainEvent.AuthenticationMethods),
            cancellationToken);

    public Task Handle(SessionRevokedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new SessionRevokedIntegrationEvent(domainEvent.UserId, domainEvent.SessionId, domainEvent.Reason),
            cancellationToken);

    public async Task Handle(OneTimeTokenIssuedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(domainEvent.UserId, cancellationToken);
        if (user is null)
        {
            return;
        }

        IntegrationEvent? integrationEvent = domainEvent.Purpose switch
        {
            OneTimeTokenPurpose.EmailVerification => new EmailVerificationRequestedIntegrationEvent(
                user.Id, domainEvent.Target ?? user.Email, user.DisplayName, domainEvent.Value, domainEvent.ExpiresAtUtc),
            OneTimeTokenPurpose.PasswordReset => new PasswordResetRequestedIntegrationEvent(
                user.Id, user.Email, user.DisplayName, domainEvent.Value, domainEvent.ExpiresAtUtc),
            OneTimeTokenPurpose.EmailChange when domainEvent.Target is not null => new EmailChangeRequestedIntegrationEvent(
                user.Id, user.Email, domainEvent.Target, user.DisplayName, domainEvent.Value, domainEvent.ExpiresAtUtc),
            OneTimeTokenPurpose.PhoneVerification when domainEvent.Target is not null => new PhoneVerificationRequestedIntegrationEvent(
                user.Id, domainEvent.Target, domainEvent.Value, domainEvent.ExpiresAtUtc),
            OneTimeTokenPurpose.SmsMfa when domainEvent.Target is not null => new SmsMfaCodeRequestedIntegrationEvent(
                user.Id, domainEvent.Target, domainEvent.Value, domainEvent.ExpiresAtUtc),
            _ => null,
        };

        if (integrationEvent is not null)
        {
            await eventBus.PublishAsync(integrationEvent, cancellationToken);
        }
    }
}
