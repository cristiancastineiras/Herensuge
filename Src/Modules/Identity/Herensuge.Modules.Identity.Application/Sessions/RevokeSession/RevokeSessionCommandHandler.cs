namespace Herensuge.Modules.Identity.Application.Sessions.RevokeSession;

internal sealed class RevokeSessionCommandHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RevokeSessionCommand>
{
    public async Task<Result> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
    {
        UserSession? session = await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null || session.UserId != command.UserId)
        {
            return SessionErrors.NotFound;
        }

        session.Revoke(SessionRevocationReasons.RevokedByUser, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
