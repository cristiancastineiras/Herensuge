namespace Herensuge.Modules.Identity.Application.Abstractions;

/// <summary>
/// Tells the client apps (their BFFs) that an SSO session has ended, so they drop their own session
/// and sign the browser out in real time. Access is already cut by then: this is only the push.
/// </summary>
public interface ISessionRevocationNotifier
{
    Task NotifyAsync(Guid sessionId, CancellationToken cancellationToken);
}
