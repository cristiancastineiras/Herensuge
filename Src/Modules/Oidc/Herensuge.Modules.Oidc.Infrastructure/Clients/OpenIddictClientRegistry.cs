using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Contracts;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Infrastructure.Clients;

/// <summary>Client applications stored by OpenIddict. Secrets are hashed by OpenIddict; plain values are returned once.</summary>
internal sealed class OpenIddictClientRegistry(IOpenIddictApplicationManager applications) : IClientRegistry
{
    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var clients = new List<ClientResponse>();
        await foreach (object application in applications.ListAsync(count: null, offset: null, cancellationToken))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, application, cancellationToken);
            // Service account clients belong to organizations (Access, plan D7), not to the platform list.
            if (!IsServiceClient(descriptor))
            {
                clients.Add(ToResponse(descriptor));
            }
        }

        return [.. clients.OrderBy(client => client.ClientId, StringComparer.Ordinal)];
    }

    public async Task<Result<string?>> CreateAsync(ClientDefinition definition, CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(definition.ClientId, cancellationToken) is not null)
        {
            return OidcErrors.ClientAlreadyExists;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = definition.ClientId,
            ClientType = definition.IsConfidential ? ClientTypes.Confidential : ClientTypes.Public,
            ApplicationType = ApplicationTypes.Web,
            ConsentType = ConsentTypes.Implicit,
        };
        Apply(descriptor, definition.Settings);

        string? secret = definition.IsConfidential ? GenerateSecret() : null;
        descriptor.ClientSecret = secret;
        await applications.CreateAsync(descriptor, cancellationToken);

        return Result.Success(secret);
    }

    public async Task<Result> UpdateAsync(string clientId, ClientSettings settings, CancellationToken cancellationToken)
    {
        (object? application, OpenIddictApplicationDescriptor? descriptor) = await FindAsync(clientId, cancellationToken);
        if (application is null || descriptor is null)
        {
            return OidcErrors.ClientNotFound;
        }

        if (IsSystem(descriptor) || IsServiceClient(descriptor))
        {
            return OidcErrors.SystemClient;
        }

        Apply(descriptor, settings);
        await applications.UpdateAsync(application, descriptor, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<string>> RotateSecretAsync(string clientId, CancellationToken cancellationToken)
    {
        (object? application, OpenIddictApplicationDescriptor? descriptor) = await FindAsync(clientId, cancellationToken);
        if (application is null || descriptor is null)
        {
            return OidcErrors.ClientNotFound;
        }

        if (IsSystem(descriptor) || IsServiceClient(descriptor))
        {
            return OidcErrors.SystemClient;
        }

        if (descriptor.ClientType != ClientTypes.Confidential)
        {
            return OidcErrors.PublicClientHasNoSecret;
        }

        string secret = GenerateSecret();
        await applications.UpdateAsync(application, secret, cancellationToken);
        return secret;
    }

    public async Task<Result> DeleteAsync(string clientId, CancellationToken cancellationToken)
    {
        (object? application, OpenIddictApplicationDescriptor? descriptor) = await FindAsync(clientId, cancellationToken);
        if (application is null || descriptor is null)
        {
            return OidcErrors.ClientNotFound;
        }

        if (IsSystem(descriptor) || IsServiceClient(descriptor))
        {
            return OidcErrors.SystemClient;
        }

        await applications.DeleteAsync(application, cancellationToken);
        return Result.Success();
    }

    public async Task<ClientLoginPage?> GetLoginPageAsync(string clientId, CancellationToken cancellationToken)
    {
        (_, OpenIddictApplicationDescriptor? descriptor) = await FindAsync(clientId, cancellationToken);
        if (descriptor is null || IsServiceClient(descriptor))
        {
            return null;
        }

        return new ClientLoginPage(descriptor.ClientId ?? clientId, descriptor.DisplayName ?? clientId, ReadLoginSettings(descriptor));
    }

    public async Task<Result> UpdateLoginSettingsAsync(string clientId, LoginSettings settings, CancellationToken cancellationToken)
    {
        (object? application, OpenIddictApplicationDescriptor? descriptor) = await FindAsync(clientId, cancellationToken);
        if (application is null || descriptor is null || IsServiceClient(descriptor))
        {
            return OidcErrors.ClientNotFound;
        }

        descriptor.Properties[OidcPropertyNames.LoginSettings] = JsonSerializer.SerializeToElement(settings, LoginSettingsJson);
        await applications.UpdateAsync(application, descriptor, cancellationToken);
        return Result.Success();
    }

    /// <summary>Stored settings, or the defaults when the client was never configured (or the JSON is unreadable).</summary>
    internal static LoginSettings ReadLoginSettings(OpenIddictApplicationDescriptor descriptor)
    {
        if (!descriptor.Properties.TryGetValue(OidcPropertyNames.LoginSettings, out JsonElement value))
        {
            return LoginSettings.Default;
        }

        try
        {
            LoginSettings? stored = value.Deserialize<LoginSettings>(LoginSettingsJson);
            LoginSettings defaults = LoginSettings.Default;
            return stored is null ? defaults : new LoginSettings(
                stored.Branding ?? defaults.Branding,
                stored.Methods ?? defaults.Methods,
                stored.AllowRegistration,
                [.. (stored.ProfileFields ?? []).Select(field => field with { Options = field.Options ?? [] })],
                stored.AllowTenantCreation);
        }
        catch (JsonException)
        {
            return LoginSettings.Default;
        }
    }

    private static readonly JsonSerializerOptions LoginSettingsJson = new(JsonSerializerDefaults.Web);

    /// <summary>Endpoint, grant and scope permissions of a browser client (code + PKCE, optional refresh).</summary>
    internal static void Apply(OpenIddictApplicationDescriptor descriptor, ClientSettings settings)
    {
        descriptor.DisplayName = settings.DisplayName;

        descriptor.RedirectUris.Clear();
        foreach (string uri in settings.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (string uri in settings.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
        ]);

        if (settings.Scopes.Contains(Scopes.OfflineAccess))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        }

        // The backend of an application asks the Access decision point with its own token (client_credentials).
        if (descriptor.ClientType == ClientTypes.Confidential && settings.Scopes.Contains(HerensugeScopes.Access))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

            // And exchanges its users' tokens for tokens of another application (plan D14).
            descriptor.Permissions.Add(Permissions.GrantTypes.TokenExchange);
        }

        foreach (string scope in settings.Scopes.Where(scope => scope != Scopes.OpenId))
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Clear();
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
    }

    private async Task<(object? Application, OpenIddictApplicationDescriptor? Descriptor)> FindAsync(string clientId, CancellationToken cancellationToken)
    {
        object? application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return (null, null);
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, cancellationToken);
        return (application, descriptor);
    }

    private static bool IsSystem(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Properties.TryGetValue(OidcPropertyNames.SystemClient, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    internal static bool IsServiceClient(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Properties.ContainsKey(OidcPropertyNames.ServiceAccountTenant);

    internal static string GenerateSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static ClientResponse ToResponse(OpenIddictApplicationDescriptor descriptor) => new(
        descriptor.ClientId ?? string.Empty,
        descriptor.DisplayName ?? string.Empty,
        descriptor.ClientType == ClientTypes.Confidential,
        [.. descriptor.RedirectUris.Select(uri => uri.OriginalString)],
        [.. descriptor.PostLogoutRedirectUris.Select(uri => uri.OriginalString)],
        [Scopes.OpenId, .. descriptor.Permissions
            .Where(permission => permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(permission => permission[Permissions.Prefixes.Scope.Length..])],
        IsSystem(descriptor));
}
