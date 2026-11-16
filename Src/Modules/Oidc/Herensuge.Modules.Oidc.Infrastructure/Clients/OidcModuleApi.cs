using System.Text.Json;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Contracts;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Infrastructure.Clients;

/// <summary>Clients of service accounts (plan D7). Access creates the aggregate around them.</summary>
internal sealed class OidcModuleApi(IOpenIddictApplicationManager applications, IOpenIddictScopeManager scopes) : IOidcModuleApi
{
    private const string ApiScopeSuffix = ".api";

    public async Task<ServiceClient> CreateServiceClientAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        string clientId = $"sa-{Guid.CreateVersion7():N}";
        string secret = OpenIddictClientRegistry.GenerateSecret();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = name,
        };
        descriptor.Permissions.UnionWith([Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials]);
        await foreach (object scope in scopes.ListAsync(count: null, offset: null, cancellationToken))
        {
            if (await scopes.GetNameAsync(scope, cancellationToken) is { } scopeName && IsApplicationScope(scopeName))
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scopeName);
            }
        }

        descriptor.Properties[OidcPropertyNames.ServiceAccountTenant] = JsonSerializer.SerializeToElement(tenantId);
        await applications.CreateAsync(descriptor, cancellationToken);
        return new ServiceClient(clientId, secret);
    }

    public async Task<string?> RotateServiceClientSecretAsync(string clientId, CancellationToken cancellationToken)
    {
        if (await FindServiceClientAsync(clientId, cancellationToken) is not { } application)
        {
            return null;
        }

        string secret = OpenIddictClientRegistry.GenerateSecret();
        await applications.UpdateAsync(application, secret, cancellationToken);
        return secret;
    }

    public async Task DeleteServiceClientAsync(string clientId, CancellationToken cancellationToken)
    {
        if (await FindServiceClientAsync(clientId, cancellationToken) is { } application)
        {
            await applications.DeleteAsync(application, cancellationToken);
        }
    }

    /// <summary>"facturas.api": an application's API, never Herensuge's own scopes.</summary>
    internal static bool IsApplicationScope(string scope) =>
        scope.EndsWith(ApiScopeSuffix, StringComparison.Ordinal) && scope is not (HerensugeScopes.Api or HerensugeScopes.Access);

    private async Task<object?> FindServiceClientAsync(string clientId, CancellationToken cancellationToken)
    {
        object? application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, cancellationToken);
        return OpenIddictClientRegistry.IsServiceClient(descriptor) ? application : null;
    }
}
