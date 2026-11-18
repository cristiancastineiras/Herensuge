namespace Herensuge.Bff;

public sealed class BffOptions
{
    public const string SectionName = "Bff";

    /// <summary>Public issuer of Herensuge (https://id.example.com/).</summary>
    public string Authority { get; set; } = "https://localhost:5001/";

    /// <summary>
    /// Optional internal address of the IdP (http://herensuge-api:8080). Back-channel calls to the
    /// Authority are sent there with X-Forwarded headers, so containers never leave the private network.
    /// </summary>
    public string? BackchannelAddress { get; set; }

    public string ClientId { get; set; } = "herensuge-portal";

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Scopes the BFF asks for: one application scope ("herensuge.api" for the portal, "fichajes.api" for Fichajes),
    /// one token, one API. No default here: the configuration binder appends to an initialized array, so a default
    /// would add herensuge.api to every application's list. appsettings.json carries the portal's.
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>Sliding lifetime of the BFF session; matches the refresh token lifetime.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Name of this BFF: prefix of its state in Valkey (sessions, locks, Data Protection keys), its Data Protection
    /// application and its telemetry service. Each application's BFF sharing Herensuge's Valkey (to hear about closed
    /// sessions and changed permissions) needs its own, e.g. "fichajes-bff".
    /// </summary>
    public string InstanceName { get; set; } = "herensuge-bff";
}

internal static class BffDefaults
{
    public const string CookieScheme = "herensuge.bff";
    public const string CookieName = "__Host-herensuge-bff";
    public const string CsrfHeader = "X-CSRF";

    // Names used to keep the tokens inside the (server-side) authentication ticket.
    public const string AccessToken = "access_token";
    public const string AccessTokenExpiresAt = "expires_at";
    public const string RefreshToken = "refresh_token";
    public const string IdentityToken = "id_token";
}
