using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithPassword;

internal sealed class SignInWithPasswordCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    SignInCompleter signInCompleter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SignInWithPasswordCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(SignInWithPasswordCommand command, CancellationToken cancellationToken) =>
        IdentityMetrics.TrackSignIn(AuthenticationMethods.Password, await HandleCoreAsync(command, cancellationToken));

    private async Task<Result<SignInResult>> HandleCoreAsync(SignInWithPasswordCommand command, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);
        User? user = email.IsSuccess ? await users.GetByEmailAsync(email.Value.Value, cancellationToken) : null;
        if (user?.PasswordHash is null)
        {
            passwordHasher.SimulateVerification(command.Password);
            return UserErrors.InvalidCredentials;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.IsLockedOut(now))
        {
            return UserErrors.LockedOut;
        }

        PasswordVerificationResult verification = passwordHasher.Verify(user.PasswordHash, command.Password);
        Result<SignInStep> step = user.SignInWithPassword(verification != PasswordVerificationResult.Failed, now);
        if (step.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return step.Error;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(passwordHasher.Hash(command.Password));
        }

        if (step.Value == SignInStep.MfaRequired)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SignInResult.MfaRequired(user, [AuthenticationMethods.Password]);
        }

        UserSession session = signInCompleter.Complete(user, [AuthenticationMethods.Password]);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SignInResult.SignedIn(session);
    }
}
