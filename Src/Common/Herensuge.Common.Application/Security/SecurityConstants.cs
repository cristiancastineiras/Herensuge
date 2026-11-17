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

    /// <summary>Roles of the app (Access role templates) in a token issued for that app's API (RFC 9068).</summary>
    public const string Roles = "roles";

    /// <summary>Permissions ("invoices:approve") granted without condition, for the token's application.</summary>
    public const string Permissions = "permissions";

    /// <summary>Permissions granted only with a condition (MFA, recent sign-in…): the API asks the PDP.</summary>
    public const string ConditionalPermissions = "conditional_permissions";

    /// <summary>Features of the application in the organization's plan (plan D5).</summary>
    public const string Entitlements = "entitlements";

    /// <summary>RFC 8693 actor: who acts on behalf of the subject (support session, token exchange).</summary>
    public const string Actor = "act";

    public const string ClientId = "client_id";
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

    /// <summary>Read-only support session opened by a platform administrator with their passkey (plan D11).</summary>
    public const string Impersonation = "imp";
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

    /// <summary>Machine token (client_credentials) of an app's backend with the herensuge.access scope: decisions and manifest.</summary>
    public const string AccessClient = "herensuge.access_client";
}

public static class HerensugeScopes
{
    public const string Api = "herensuge.api";
    public const string ApiResource = "herensuge-api";

    /// <summary>Lets an app's backend call the Access decision API (client_credentials). Audience: herensuge-api.</summary>
    public const string Access = "herensuge.access";
}
