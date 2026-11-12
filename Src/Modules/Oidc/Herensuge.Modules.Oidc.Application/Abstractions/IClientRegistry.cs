namespace Herensuge.Modules.Oidc.Application.Abstractions;

/// <summary>OIDC client applications (implemented with OpenIddict's application manager).</summary>
public interface IClientRegistry
{
    Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken cancellationToken);

    /// <returns>The generated secret for confidential clients, null for public ones.</returns>
    Task<Result<string?>> CreateAsync(ClientDefinition definition, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(string clientId, ClientSettings settings, CancellationToken cancellationToken);

    Task<Result<string>> RotateSecretAsync(string clientId, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(string clientId, CancellationToken cancellationToken);
}

public sealed record ClientDefinition(string ClientId, bool IsConfidential, ClientSettings Settings);

public sealed record ClientSettings(
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes);

public sealed record ClientResponse(
    string ClientId,
    string DisplayName,
    bool IsConfidential,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    bool IsSystem);
