namespace Herensuge.Modules.Identity.Application.Sessions.ValidateSession;

internal sealed class ValidateSessionCommandHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ValidateSessionCommand>
{
    public async Task<Result> Handle(ValidateSessionCommand command, CancellationToken cancellationToken)
    {
        UserSession? session = await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null || session.UserId != command.UserId || !session.Touch(timeProvider.GetUtcNow().UtcDateTime))
        {
            return SessionErrors.Inactive;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
