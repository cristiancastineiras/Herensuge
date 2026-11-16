using Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmPhoneVerification;

internal sealed class ConfirmPhoneVerificationCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IOneTimeTokenRepository tokens,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<ConfirmPhoneVerificationCommand, RecoveryCodesResponse>
{
    public async Task<Result<RecoveryCodesResponse>> Handle(ConfirmPhoneVerificationCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        OneTimeToken? token = await tokens.GetLatestActiveAsync(user.Id, OneTimeTokenPurpose.PhoneVerification, now, cancellationToken);
        if (token is null)
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        Result consumed = token.Consume(command.Code, hasher, now);
        Result<PhoneNumber> phoneNumber = PhoneNumber.Create(token.Target);
        if (consumed.IsFailure || phoneNumber.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        bool hadMfa = user.HasMfa;
        Result<IReadOnlyList<string>> codes = user.SetVerifiedPhoneNumber(phoneNumber.Value, hasher, now);

        // Turning MFA on closes the sessions opened without it (other browsers and devices): they sign in again with it.
        if (!hadMfa)
        {
            await sessions.RevokeOthersAsync(user.Id, command.CurrentSessionId, SessionRevocationReasons.MfaEnabled, now, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResponse(codes.Value);
    }
}
