using Herensuge.Common.Application.Messaging;
using Herensuge.Modules.Identity.Contracts;
using Herensuge.Modules.Identity.Contracts.IntegrationEvents;
using Herensuge.Modules.Notifications.Application.Abstractions;
using Herensuge.Modules.Notifications.Application.Templates;
using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// Turns integration events into emails and SMS. Delivery is at-least-once: a crash right after
/// sending may resend the same message, never lose it.
/// </summary>
internal sealed class SendNotificationsHandler(
    IEmailSender emailSender,
    ISmsSender smsSender,
    IIdentityModuleApi identity,
    IOptions<NotificationOptions> options) :
    IIntegrationEventHandler<EmailVerificationRequestedIntegrationEvent>,
    IIntegrationEventHandler<PasswordResetRequestedIntegrationEvent>,
    IIntegrationEventHandler<EmailChangeRequestedIntegrationEvent>,
    IIntegrationEventHandler<PasswordChangedIntegrationEvent>,
    IIntegrationEventHandler<MfaEnabledIntegrationEvent>,
    IIntegrationEventHandler<MfaDisabledIntegrationEvent>,
    IIntegrationEventHandler<PasskeyAddedIntegrationEvent>,
    IIntegrationEventHandler<PasskeyRemovedIntegrationEvent>,
    IIntegrationEventHandler<ExternalLoginLinkedIntegrationEvent>,
    IIntegrationEventHandler<NewDeviceSignedInIntegrationEvent>,
    IIntegrationEventHandler<UserLockedOutIntegrationEvent>,
    IIntegrationEventHandler<PhoneVerificationRequestedIntegrationEvent>,
    IIntegrationEventHandler<SmsMfaCodeRequestedIntegrationEvent>,
    IIntegrationEventHandler<InvitationCreatedIntegrationEvent>
{
    private NotificationOptions Settings => options.Value;

    public Task Handle(EmailVerificationRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        emailSender.SendAsync(
            EmailVerificationEmail.Create(
                integrationEvent.Email,
                integrationEvent.DisplayName,
                Settings.Link($"verify-email?token={Uri.EscapeDataString(integrationEvent.Token)}"),
                integrationEvent.ExpiresAtUtc,
                Settings.ProductName),
            cancellationToken);

    public Task Handle(PasswordResetRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        emailSender.SendAsync(
            PasswordResetEmail.Create(
                integrationEvent.Email,
                integrationEvent.DisplayName,
                Settings.Link($"reset-password?token={Uri.EscapeDataString(integrationEvent.Token)}"),
                integrationEvent.ExpiresAtUtc,
                Settings.ProductName),
            cancellationToken);

    public async Task Handle(EmailChangeRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await emailSender.SendAsync(
            EmailChangeConfirmationEmail.Create(
                integrationEvent.NewEmail,
                integrationEvent.DisplayName,
                Settings.Link($"confirm-email?token={Uri.EscapeDataString(integrationEvent.Token)}"),
                integrationEvent.ExpiresAtUtc,
                Settings.ProductName),
            cancellationToken);

        await emailSender.SendAsync(
            EmailChangeRequestedNoticeEmail.Create(integrationEvent.CurrentEmail, integrationEvent.DisplayName, integrationEvent.NewEmail, Settings.ProductName),
            cancellationToken);
    }

    public Task Handle(PasswordChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, "La contraseña de tu cuenta se ha cambiado y se han cerrado tus otras sesiones.", cancellationToken);

    public Task Handle(MfaEnabledIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, $"Se ha activado la verificación en dos pasos ({MethodName(integrationEvent.Method)}).", cancellationToken);

    public Task Handle(MfaDisabledIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, $"Se ha desactivado un método de verificación en dos pasos ({MethodName(integrationEvent.Method)}).", cancellationToken);

    public Task Handle(PasskeyAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, $"Se ha añadido una passkey a tu cuenta ({integrationEvent.Name}).", cancellationToken);

    public Task Handle(PasskeyRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, "Se ha eliminado una passkey de tu cuenta.", cancellationToken);

    public Task Handle(ExternalLoginLinkedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        SendSecurityNoticeAsync(integrationEvent.UserId, $"Se ha vinculado una cuenta de {ProviderName(integrationEvent.Provider)} a tu cuenta.", cancellationToken);

    public async Task Handle(NewDeviceSignedInIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        UserSnapshot? user = await identity.GetUserAsync(integrationEvent.UserId, cancellationToken);
        if (user is null)
        {
            return;
        }

        await emailSender.SendAsync(
            NewDeviceEmail.Create(user.Email, user.DisplayName, integrationEvent.IpAddress, integrationEvent.UserAgent, integrationEvent.OccurredOnUtc, Settings.ProductName),
            cancellationToken);
    }

    public async Task Handle(UserLockedOutIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        UserSnapshot? user = await identity.GetUserAsync(integrationEvent.UserId, cancellationToken);
        if (user is null)
        {
            return;
        }

        await emailSender.SendAsync(
            AccountLockedEmail.Create(user.Email, user.DisplayName, integrationEvent.LockedUntilUtc, Settings.Link("forgot-password"), Settings.ProductName),
            cancellationToken);
    }

    public Task Handle(PhoneVerificationRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        smsSender.SendAsync(PhoneVerificationSms.Create(integrationEvent.PhoneNumber, integrationEvent.Code, Settings.ProductName), cancellationToken);

    public Task Handle(SmsMfaCodeRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        smsSender.SendAsync(MfaCodeSms.Create(integrationEvent.PhoneNumber, integrationEvent.Code, Settings.ProductName), cancellationToken);

    public async Task Handle(InvitationCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        UserSnapshot? inviter = await identity.GetUserAsync(integrationEvent.InvitedByUserId, cancellationToken);
        await emailSender.SendAsync(
            InvitationEmail.Create(
                integrationEvent.Email,
                inviter?.DisplayName ?? "Un administrador",
                integrationEvent.TenantName,
                integrationEvent.Role,
                Settings.Link($"invitations/{Uri.EscapeDataString(integrationEvent.Token)}"),
                integrationEvent.ExpiresAtUtc,
                Settings.ProductName),
            cancellationToken);
    }

    private async Task SendSecurityNoticeAsync(Guid userId, string change, CancellationToken cancellationToken)
    {
        UserSnapshot? user = await identity.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        await emailSender.SendAsync(SecurityNoticeEmail.Create(user.Email, user.DisplayName, change, Settings.ProductName), cancellationToken);
    }

    private static string MethodName(string method) => method switch
    {
        "totp" => "aplicación de autenticación",
        "sms" => "SMS",
        _ => method,
    };

    private static string ProviderName(string provider) => provider switch
    {
        "google" => "Google",
        "microsoft" => "Microsoft",
        _ => provider,
    };
}
