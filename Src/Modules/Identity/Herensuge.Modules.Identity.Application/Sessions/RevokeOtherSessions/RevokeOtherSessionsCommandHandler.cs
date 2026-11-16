using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Sessions.RevokeOtherSessions;

internal sealed class RevokeOtherSessionsCommandHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RevokeOtherSessionsCommand>
{
    public async Task<Result> Handle(RevokeOtherSessionsCommand command, CancellationToken cancellationToken)
    {
        await sessions.RevokeOthersAsync(
            command.UserId, command.CurrentSessionId, SessionRevocationReasons.RevokedByUser, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
