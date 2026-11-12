using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Herensuge.Common.Domain;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Abstractions;
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
            clients.Add(ToResponse(descriptor));
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

        if (IsSystem(descriptor))
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

        if (IsSystem(descriptor))
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

        if (IsSystem(descriptor))
        {
            return OidcErrors.SystemClient;
        }

        await applications.DeleteAsync(application, cancellationToken);
        return Result.Success();
    }

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

    private static string GenerateSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

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
