using System.Globalization;
using Herensuge.Modules.Notifications.Application.Abstractions;

namespace Herensuge.Modules.Notifications.Application.Templates;

// Plain-text templates in Spanish, one class each. Times are shown in UTC: the user's time zone is unknown.
internal static class TemplateFormat
{
    public static string Utc(DateTime value) =>
        value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (UTC)";
}

internal static class EmailVerificationEmail
{
    public static EmailMessage Create(string to, string name, string link, DateTime expiresAtUtc, string product) => new(
        to,
        "Verifica tu email",
        $"""
        Hola {name},

        Confirma tu dirección de email para activar tu cuenta de {product}:
        {link}

        El enlace caduca el {TemplateFormat.Utc(expiresAtUtc)}.
        Si no has creado esta cuenta, ignora este mensaje.
        """);
}

internal static class PasswordResetEmail
{
    public static EmailMessage Create(string to, string name, string link, DateTime expiresAtUtc, string product) => new(
        to,
        "Restablece tu contraseña",
        $"""
        Hola {name},

        Hemos recibido una solicitud para restablecer la contraseña de tu cuenta de {product}:
        {link}

        El enlace caduca el {TemplateFormat.Utc(expiresAtUtc)} y solo se puede usar una vez.
        Si no lo has pedido tú, ignora este mensaje: tu contraseña no cambiará.
        """);
}

internal static class EmailChangeConfirmationEmail
{
    public static EmailMessage Create(string to, string name, string link, DateTime expiresAtUtc, string product) => new(
        to,
        "Confirma tu nuevo email",
        $"""
        Hola {name},

        Confirma que quieres usar esta dirección en tu cuenta de {product}:
        {link}

        El enlace caduca el {TemplateFormat.Utc(expiresAtUtc)}.
        Si no lo has pedido tú, ignora este mensaje.
        """);
}

internal static class EmailChangeRequestedNoticeEmail
{
    public static EmailMessage Create(string to, string name, string newEmail, string product) => new(
        to,
        "Solicitud de cambio de email",
        $"""
        Hola {name},

        Se ha solicitado cambiar el email de tu cuenta de {product} a {newEmail}.
        Si no has sido tú, cambia tu contraseña y revisa tus sesiones abiertas cuanto antes.
        """);
}

internal static class SecurityNoticeEmail
{
    public static EmailMessage Create(string to, string name, string change, string product) => new(
        to,
        "Aviso de seguridad",
        $"""
        Hola {name},

        {change}

        Si has sido tú, no tienes que hacer nada.
        Si no, cambia tu contraseña y revisa tus sesiones abiertas en {product} cuanto antes.
        """);
}

internal static class NewDeviceEmail
{
    public static EmailMessage Create(string to, string name, string? ipAddress, string? userAgent, DateTime occurredAtUtc, string product) => new(
        to,
        "Nuevo inicio de sesión",
        $"""
        Hola {name},

        Se ha iniciado sesión en tu cuenta de {product} desde un dispositivo nuevo.

        Fecha: {TemplateFormat.Utc(occurredAtUtc)}
        IP: {ipAddress ?? "desconocida"}
        Navegador: {userAgent ?? "desconocido"}

        Si no has sido tú, cambia tu contraseña y cierra tus sesiones abiertas cuanto antes.
        """);
}

internal static class AccountLockedEmail
{
    public static EmailMessage Create(string to, string name, DateTime lockedUntilUtc, string resetLink, string product) => new(
        to,
        "Cuenta bloqueada temporalmente",
        $"""
        Hola {name},

        Tras varios intentos fallidos, hemos bloqueado tu cuenta de {product} hasta el {TemplateFormat.Utc(lockedUntilUtc)}.

        Si no has sido tú, alguien podría estar intentando entrar. Te recomendamos cambiar tu contraseña:
        {resetLink}
        """);
}

internal static class InvitationEmail
{
    public static EmailMessage Create(string to, string inviter, string tenantName, string role, string link, DateTime expiresAtUtc, string product) => new(
        to,
        $"Invitación a {tenantName}",
        $"""
        Hola,

        {inviter} te ha invitado a unirte a {tenantName} en {product} con el rol {RoleName(role)}.

        Acepta la invitación aquí:
        {link}

        La invitación caduca el {TemplateFormat.Utc(expiresAtUtc)}.
        """);

    private static string RoleName(string role) => role switch
    {
        "owner" => "propietario",
        "admin" => "administrador",
        _ => "miembro",
    };
}

internal static class PhoneVerificationSms
{
    public static SmsMessage Create(string to, string code, string product) =>
        new(to, $"Tu código para verificar el teléfono en {product} es {code}. Caduca en 10 minutos.");
}

internal static class MfaCodeSms
{
    public static SmsMessage Create(string to, string code, string product) =>
        new(to, $"Tu código de acceso a {product} es {code}. Caduca en 5 minutos. No lo compartas con nadie.");
}
