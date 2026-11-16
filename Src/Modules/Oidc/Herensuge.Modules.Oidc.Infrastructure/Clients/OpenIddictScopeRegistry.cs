using Herensuge.Modules.Oidc.Application.Abstractions;
using OpenIddict.Abstractions;

namespace Herensuge.Modules.Oidc.Infrastructure.Clients;

internal sealed class OpenIddictScopeRegistry(IOpenIddictScopeManager scopes) : IScopeRegistry
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
}
