using Herensuge.Modules.Identity.Domain.Sessions.Events;

namespace Herensuge.Modules.Identity.Application.EventHandlers;

/// <summary>Pushes every closed session to the client apps (sign-out, closed from another device, password changed...).</summary>
internal sealed class NotifySessionRevokedHandler(ISessionRevocationNotifier notifier) : IDomainEventHandler<SessionRevokedDomainEvent>
{
    public Task Handle(SessionRevokedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(domainEvent.SessionId, cancellationToken);
}
