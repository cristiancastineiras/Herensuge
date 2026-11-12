using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Mfa.StartPhoneVerification;

internal sealed class StartPhoneVerificationCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<StartPhoneVerificationCommand>
{
    public async Task<Result> Handle(StartPhoneVerificationCommand command, CancellationToken cancellationToken)
    {
        Result<PhoneNumber> phoneNumber = PhoneNumber.Create(command.PhoneNumber);
        if (phoneNumber.IsFailure)
        {
            return phoneNumber.Error;
        }

        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result issued = await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.PhoneVerification, phoneNumber.Value.Value, cancellationToken);
        if (issued.IsFailure)
        {
            return issued.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
