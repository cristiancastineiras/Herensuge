namespace Herensuge.Modules.Identity.Application.SignIn.SignOut;

/// <summary>Revokes the SSO session; SessionRevoked then revokes its OIDC tokens.</summary>
internal sealed class SignOutCommandHandler(
    IUserSessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SignOutCommand>
{
    public async Task<Result> Handle(SignOutCommand command, CancellationToken cancellationToken)
    {
        UserSession? session = await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Success();
        }

        session.Revoke(SessionRevocationReasons.SignOut, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
