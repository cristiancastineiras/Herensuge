namespace Herensuge.Modules.Identity.Application.Sessions.RevokeOtherSessions;

internal sealed class RevokeOtherSessionsCommandHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RevokeOtherSessionsCommand>
{
    public async Task<Result> Handle(RevokeOtherSessionsCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<UserSession> active = await sessions.GetActiveByUserAsync(command.UserId, now, cancellationToken);

        foreach (UserSession session in active.Where(session => session.Id != command.CurrentSessionId))
        {
            session.Revoke(SessionRevocationReasons.RevokedByUser, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
