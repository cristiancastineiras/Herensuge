namespace Herensuge.Common.Application.Security;

public static class HerensugeClaimTypes
{
    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string Name = "name";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string TenantId = "tenant_id";
    public const string TenantRole = "tenant_role";
    public const string AuthenticationMethod = "amr";
    public const string AuthenticationTime = "auth_time";
    public const string Role = "role";
}

/// <summary>Authentication method references (RFC 8176).</summary>
public static class AuthenticationMethods
{
    public const string Password = "pwd";
    public const string Otp = "otp";
    public const string Sms = "sms";
    public const string Passkey = "pop";
    public const string MultiFactor = "mfa";
    public const string Federated = "fed";
}

public static class PlatformRoles
{
    public const string PlatformAdmin = "platform_admin";
}

public static class HerensugeAuthenticationSchemes
{
    public const string IdentityProvider = "herensuge.idp";
    public const string MfaPending = "herensuge.mfa";
}

public static class HerensugeCookies
{
    public const string IdentityProvider = "__Host-herensuge-idp";
    public const string MfaPending = "__Host-herensuge-mfa";
    public const string Device = "__Host-herensuge-device";
}

public static class HerensugePolicies
{
    /// <summary>Bearer access token issued by Herensuge with the herensuge.api scope.</summary>
    public const string Api = "herensuge.api";

    /// <summary>Api + platform_admin role + passkey authentication.</summary>
    public const string PlatformAdmin = "herensuge.platform_admin";
}

public static class HerensugeScopes
{
    public const string Api = "herensuge.api";
    public const string ApiResource = "herensuge-api";
}
