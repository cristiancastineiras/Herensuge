namespace Herensuge.Modules.Oidc.Contracts;

/// <summary>
/// For other modules:
/// <list type="bullet">
/// <item>the login page of each client (Identity checks registration against it);</item>
/// <item>OIDC clients of service accounts (plan D7), for Access: confidential, client_credentials only, with the scopes
/// of the applications ("facturas.api") but never herensuge.api or herensuge.access. The platform cannot edit them.</item>
/// </list>
/// </summary>
public interface IOidcModuleApi
{
    /// <summary>Login page of a client; null when the client does not exist or is a service account.</summary>
    Task<LoginSettings?> GetLoginSettingsAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>A new client for an organization's service account. The secret is returned only here.</summary>
    Task<ServiceClient> CreateServiceClientAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    /// <summary>A new secret (the old one stops working at once); null when the client does not exist.</summary>
    Task<string?> RotateServiceClientSecretAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Idempotent.</summary>
    Task DeleteServiceClientAsync(string clientId, CancellationToken cancellationToken);
}

public sealed record ServiceClient(string ClientId, string ClientSecret);
