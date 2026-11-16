using Herensuge.Modules.Access.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Oidc.Application.EventHandlers;

/// <summary>
/// An application synced its manifest: its scope ("facturas.api") gives tokens for its API ("facturas-api"), also to
/// the organizations' service accounts.
/// </summary>
internal sealed class RegisterApplicationScopeHandler(IScopeRegistry scopes) : IIntegrationEventHandler<ResourceServerSyncedIntegrationEvent>
{
    public async Task Handle(ResourceServerSyncedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await scopes.EnsureAsync(integrationEvent.Scope, integrationEvent.Audience, cancellationToken);
        await scopes.AllowForServiceClientsAsync(integrationEvent.Scope, cancellationToken);
    }
}
