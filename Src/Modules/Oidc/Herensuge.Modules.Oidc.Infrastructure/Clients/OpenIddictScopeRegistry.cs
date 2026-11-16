using Herensuge.Modules.Oidc.Application.Abstractions;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Infrastructure.Clients;

internal sealed class OpenIddictScopeRegistry(IOpenIddictScopeManager scopes, IOpenIddictApplicationManager applications) : IScopeRegistry
{
    public async Task EnsureAsync(string scope, string resource, CancellationToken cancellationToken)
    {
        object? existing = await scopes.FindByNameAsync(scope, cancellationToken);
        var descriptor = new OpenIddictScopeDescriptor();
        if (existing is not null)
        {
            await scopes.PopulateAsync(descriptor, existing, cancellationToken);
            if (descriptor.Resources.Count == 1 && descriptor.Resources.Contains(resource))
            {
                return;
            }
        }

        descriptor.Name = scope;
        descriptor.DisplayName ??= scope;
        descriptor.Resources.Clear();
        descriptor.Resources.Add(resource);

        if (existing is null)
        {
            await scopes.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await scopes.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    // shortcut: walks every client (fine for hundreds of service accounts); index them by property past a few thousand.
    public async Task AllowForServiceClientsAsync(string scope, CancellationToken cancellationToken)
    {
        if (!OidcModuleApi.IsApplicationScope(scope))
        {
            return;
        }

        await foreach (object application in applications.ListAsync(count: null, offset: null, cancellationToken))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, application, cancellationToken);
            if (OpenIddictClientRegistry.IsServiceClient(descriptor) && descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope))
            {
                await applications.UpdateAsync(application, descriptor, cancellationToken);
            }
        }
    }
}
