using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmTotpSetup;

internal sealed class ConfirmTotpSetupCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    ITotpService totpService,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<ConfirmTotpSetupCommand, ConfirmTotpSetupResponse>
{
    public async Task<Result<ConfirmTotpSetupResponse>> Handle(ConfirmTotpSetupCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.PendingTotpSecret is null)
        {
            return UserErrors.TotpSetupNotStarted;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        bool hadMfa = user.HasMfa;
        TotpVerification verification = totpService.Verify(user.PendingTotpSecret, command.Code);
        Result<IReadOnlyList<string>> confirmed = user.ConfirmTotpSetup(verification.IsValid, verification.TimeStep, hasher, now);
        if (confirmed.IsFailure)
        {
            return confirmed.Error;
        }

        // Proving possession of the new authenticator upgrades the current sign-in to MFA.
        UserSession? session = null;
        if (command.StepUpSessionId is { } sessionId)
        {
            session = await sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is not null && session.UserId == user.Id && session.IsActive(now))
            {
                session.StepUp([AuthenticationMethods.Otp, AuthenticationMethods.MultiFactor], now);
            }
            else
            {
                session = null;
            }
        }

        // Turning MFA on closes the sessions opened without it (other browsers and devices): they sign in again with it.
        if (!hadMfa)
        {
            await sessions.RevokeOthersAsync(user.Id, session?.Id, SessionRevocationReasons.MfaEnabled, now, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConfirmTotpSetupResponse(confirmed.Value, session?.AuthenticationMethods, session?.AuthenticatedAtUtc);
    }
}
