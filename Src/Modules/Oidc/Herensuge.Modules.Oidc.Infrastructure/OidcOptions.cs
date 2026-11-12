namespace Herensuge.Modules.Oidc.Infrastructure;

public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    /// <summary>Public URL of the IdP (https://id.example.com/). Fixed so tokens do not depend on the request host.</summary>
    public string Issuer { get; set; } = "https://localhost:5001/";

    public OidcKeyOptions Keys { get; set; } = new();

    public PortalClientOptions PortalClient { get; set; } = new();
}

public sealed class OidcKeyOptions
{
    /// <summary>Folder holding signing.pfx and encryption.pfx (relative to the content root unless absolute).</summary>
    public string Directory { get; set; } = "App_Data/oidc-keys";

    /// <summary>Password protecting both PFX files. Strongly recommended in production.</summary>
    public string? Password { get; set; }

    /// <summary>Generate self-signed RSA 3072 certificates (valid 2 years) when the files do not exist.</summary>
    public bool AutoGenerate { get; set; } = true;
}

/// <summary>The confidential client used by Herensuge.Bff. Created and kept in sync at startup.</summary>
public sealed class PortalClientOptions
{
    public string ClientId { get; set; } = "herensuge-portal";

    public string? ClientSecret { get; set; }

    public string[] RedirectUris { get; set; } = ["https://localhost:5002/bff/callback"];

    public string[] PostLogoutRedirectUris { get; set; } = ["https://localhost:5002/bff/signout-callback"];
}
