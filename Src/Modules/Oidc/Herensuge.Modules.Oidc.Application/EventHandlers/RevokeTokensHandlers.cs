using Herensuge.Modules.Identity.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Oidc.Application.EventHandlers;

/// <summary>A revoked SSO session takes its authorizations and tokens with it (idempotent).</summary>
internal sealed class RevokeTokensOnSessionRevokedHandler(ITokenRevoker tokenRevoker)
    : IIntegrationEventHandler<SessionRevokedIntegrationEvent>
{
    public Task Handle(SessionRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        tokenRevoker.RevokeSessionAsync(integrationEvent.UserId, integrationEvent.SessionId, cancellationToken);
}

internal sealed class RevokeTokensOnUserDeletedHandler(ITokenRevoker tokenRevoker)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        tokenRevoker.RevokeUserAsync(integrationEvent.UserId, cancellationToken);
}
