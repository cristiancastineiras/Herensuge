namespace Herensuge.Modules.Identity.Application;

/// <summary>Google and Microsoft sign-in. A provider is offered only when its credentials are configured.</summary>
public sealed class ExternalProvidersOptions
{
    public const string SectionName = "Identity:ExternalProviders";

    public ExternalProviderSettings Google { get; set; } = new();

    public ExternalProviderSettings Microsoft { get; set; } = new();

    public IReadOnlyList<string> GetConfiguredProviders()
    {
        var providers = new List<string>(2);
        if (Google.IsConfigured)
        {
            providers.Add(ExternalProviders.Google);
        }

        if (Microsoft.IsConfigured)
        {
            providers.Add(ExternalProviders.Microsoft);
        }

        return providers;
    }
}

public sealed class ExternalProviderSettings
{
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>Microsoft only: common (personal and work accounts), organizations or a tenant id.</summary>
    public string? Tenant { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public static class ExternalProviders
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
}
