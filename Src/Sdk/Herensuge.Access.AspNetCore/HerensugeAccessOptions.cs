namespace Herensuge.Access.AspNetCore;

/// <summary>The application as Herensuge knows it: its manifest key and its confidential OIDC client.</summary>
public sealed class HerensugeAccessOptions
{
    /// <summary>Herensuge's issuer URL, e.g. https://id.example.com/.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>"resourceServer" of the manifest (herensuge.access.json). The API accepts tokens for "{key}-api".</summary>
    public string ResourceServer { get; set; } = string.Empty;

    /// <summary>Client of the application (clientId of the manifest), with the herensuge.access scope.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Timeout of each call to the decision point; an unanswered call denies.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional internal address of Herensuge (http://herensuge-api:8080). Calls to <see cref="Authority"/> (discovery,
    /// JWKS, tokens, decision point, userinfo) go there with X-Forwarded-Proto/Host, so a container never leaves the
    /// private network. The issuer stays the public one.
    /// </summary>
    public string? BackchannelAddress { get; set; }

    internal string Audience => $"{ResourceServer}-api";

    internal void Validate()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Herensuge access: Authority must be an absolute URL.");
        }

        if (!string.IsNullOrWhiteSpace(BackchannelAddress) && !Uri.TryCreate(BackchannelAddress, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Herensuge access: BackchannelAddress must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(ResourceServer) || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("Herensuge access: ResourceServer, ClientId and ClientSecret are required.");
        }
    }
}
