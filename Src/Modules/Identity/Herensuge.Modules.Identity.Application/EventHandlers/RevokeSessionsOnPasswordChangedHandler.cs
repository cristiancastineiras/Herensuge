using Herensuge.Modules.Identity.Domain.Users.Events;

namespace Herensuge.Modules.Identity.Application.EventHandlers;

/// <summary>
/// A new password closes every other session (and, through SessionRevoked, their tokens). This handler runs a
/// moment later (outbox), so sessions started after the change, such as the sign-in right after a reset, stay open.
/// </summary>
internal sealed class RevokeSessionsOnPasswordChangedHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDomainEventHandler<PasswordChangedDomainEvent>
{
    public async Task Handle(PasswordChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<UserSession> active = await sessions.GetActiveByUserAsync(domainEvent.UserId, now, cancellationToken);

        foreach (UserSession session in active.Where(session =>
                     session.Id != domainEvent.KeepSessionId && session.CreatedAtUtc <= domainEvent.ChangedAtUtc))
        {
            session.Revoke(SessionRevocationReasons.PasswordChanged, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
