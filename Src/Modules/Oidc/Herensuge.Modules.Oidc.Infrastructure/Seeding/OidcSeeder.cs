using System.Text.Json;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Infrastructure.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Infrastructure.Seeding;

/// <summary>
/// At startup: creates the herensuge.api scope and creates or synchronizes the portal client with the
/// configuration (redirect URIs, permissions and secret). Fails fast if the database is not migrated.
/// </summary>
internal sealed class OidcSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<OidcOptions> options,
    ILogger<OidcSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await EnsureApiScopeAsync(scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>(), cancellationToken);
        await EnsurePortalClientAsync(scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureApiScopeAsync(IOpenIddictScopeManager scopes, CancellationToken cancellationToken)
    {
        if (await scopes.FindByNameAsync(HerensugeScopes.Api, cancellationToken) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = HerensugeScopes.Api,
            DisplayName = "Herensuge API",
        };
        descriptor.Resources.Add(HerensugeScopes.ApiResource);
        await scopes.CreateAsync(descriptor, cancellationToken);
    }

    private async Task EnsurePortalClientAsync(IOpenIddictApplicationManager applications, CancellationToken cancellationToken)
    {
        PortalClientOptions portal = options.Value.PortalClient;
        if (string.IsNullOrWhiteSpace(portal.ClientSecret))
        {
            logger.LogWarning("Oidc:PortalClient:ClientSecret is empty: the {ClientId} client was not created or updated.", portal.ClientId);
            return;
        }

        object? application = await applications.FindByClientIdAsync(portal.ClientId, cancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor();
        if (application is not null)
        {
            await applications.PopulateAsync(descriptor, application, cancellationToken);
        }

        descriptor.ClientId = portal.ClientId;
        descriptor.ClientType = ClientTypes.Confidential;
        descriptor.ApplicationType = ApplicationTypes.Web;
        descriptor.ConsentType = ConsentTypes.Implicit;
        descriptor.Properties[OidcPropertyNames.SystemClient] = JsonSerializer.SerializeToElement(true);
        OpenIddictClientRegistry.Apply(descriptor, new ClientSettings(
            "Herensuge Portal",
            portal.RedirectUris,
            portal.PostLogoutRedirectUris,
            [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, HerensugeScopes.Api]));

        if (application is null)
        {
            descriptor.ClientSecret = portal.ClientSecret;
            await applications.CreateAsync(descriptor, cancellationToken);
            logger.LogInformation("Created the {ClientId} client", portal.ClientId);
            return;
        }

        await applications.UpdateAsync(application, descriptor, cancellationToken);
        if (!await applications.ValidateClientSecretAsync(application, portal.ClientSecret, cancellationToken))
        {
            await applications.UpdateAsync(application, portal.ClientSecret, cancellationToken);
            logger.LogInformation("Updated the secret of the {ClientId} client", portal.ClientId);
        }
    }
}
