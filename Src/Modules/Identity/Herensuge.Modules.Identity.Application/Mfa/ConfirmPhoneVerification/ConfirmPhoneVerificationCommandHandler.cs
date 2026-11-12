using Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmPhoneVerification;

internal sealed class ConfirmPhoneVerificationCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository tokens,
    IUnitOfWork unitOfWork,
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

        Result consumed = token.Consume(command.Code, now);
        Result<PhoneNumber> phoneNumber = PhoneNumber.Create(token.Target);
        if (consumed.IsFailure || phoneNumber.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        Result<IReadOnlyList<string>> codes = user.SetVerifiedPhoneNumber(phoneNumber.Value, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResponse(codes.Value);
    }
}
