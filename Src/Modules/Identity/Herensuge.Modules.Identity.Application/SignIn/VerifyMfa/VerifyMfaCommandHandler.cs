using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.VerifyMfa;

internal sealed class VerifyMfaCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IOneTimeTokenRepository tokens,
    ITotpService totpService,
    SignInCompleter signInCompleter,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<VerifyMfaCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(VerifyMfaCommand command, CancellationToken cancellationToken) =>
        IdentityMetrics.TrackMfa(command.Method, await HandleCoreAsync(command, cancellationToken));

    private async Task<Result<SignInResult>> HandleCoreAsync(VerifyMfaCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.IsLockedOut(now))
        {
            return UserErrors.LockedOut;
        }

        (Result verification, string methodReference) = command.Method switch
        {
            MfaMethods.Totp => (VerifyTotp(user, command.Code, now), AuthenticationMethods.Otp),
            MfaMethods.RecoveryCode => (user.RedeemRecoveryCode(command.Code, hasher, now), AuthenticationMethods.Otp),
            MfaMethods.Sms => (await VerifySmsAsync(user, command.Code, now, cancellationToken), AuthenticationMethods.Sms),
            _ => (Result.Failure(UserErrors.MfaMethodNotAvailable), string.Empty),
        };

        if (verification.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return verification.Error;
        }

        string[] methods = [.. command.FirstFactorMethods, methodReference, AuthenticationMethods.MultiFactor];

        UserSession session;
        if (command.StepUpSessionId is { } sessionId)
        {
            UserSession? existing = await sessions.GetByIdAsync(sessionId, cancellationToken);
            if (existing is null || existing.UserId != user.Id || !existing.IsActive(now))
            {
                return SessionErrors.Inactive;
            }

            user.CompleteSignIn();
            existing.StepUp(methods, now);
            session = existing;
        }
        else
        {
            session = signInCompleter.Complete(user, methods);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SignInResult.SignedIn(session);
    }

    private Result VerifyTotp(User user, string code, DateTime now)
    {
        if (user.TotpSecret is null)
        {
            return UserErrors.MfaMethodNotAvailable;
        }

        TotpVerification verification = totpService.Verify(user.TotpSecret, code);
        return user.VerifyTotpCode(verification.IsValid, verification.TimeStep, now);
    }

    private async Task<Result> VerifySmsAsync(User user, string code, DateTime now, CancellationToken cancellationToken)
    {
        if (!user.HasVerifiedPhone)
        {
            return UserErrors.MfaMethodNotAvailable;
        }

        OneTimeToken? token = await tokens.GetLatestActiveAsync(user.Id, OneTimeTokenPurpose.SmsMfa, now, cancellationToken);
        if (token is null || token.Consume(code, hasher, now).IsFailure)
        {
            return user.RegisterFailedSmsCode(now);
        }

        return Result.Success();
    }
}
