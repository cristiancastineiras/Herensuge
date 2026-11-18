namespace Herensuge.Modules.Oidc.Application;

/// <summary>Custom properties stored in OpenIddict entries.</summary>
public static class OidcPropertyNames
{
    /// <summary>SSO session an authorization was issued from (used to revoke it with the session).</summary>
    public const string SessionId = "herensuge.sid";

    public const string TenantId = "herensuge.tenant_id";

    /// <summary>Clients managed through configuration: the platform API cannot change them.</summary>
    public const string SystemClient = "herensuge.system";

    /// <summary>Clients of service accounts (plan D7), managed by Access: the organization they belong to.</summary>
    public const string ServiceAccountTenant = "herensuge.service_account_tenant";

    /// <summary>Login page of the client (LoginSettings as JSON); absent until the platform configures it.</summary>
    public const string LoginSettings = "herensuge.login";
}
