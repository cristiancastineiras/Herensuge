namespace Herensuge.Modules.Oidc.Application;

/// <summary>Custom properties stored in OpenIddict entries.</summary>
public static class OidcPropertyNames
{
    /// <summary>SSO session an authorization was issued from (used to revoke it with the session).</summary>
    public const string SessionId = "herensuge.sid";

    public const string TenantId = "herensuge.tenant_id";

    /// <summary>Clients managed through configuration: the platform API cannot change them.</summary>
    public const string SystemClient = "herensuge.system";
}
