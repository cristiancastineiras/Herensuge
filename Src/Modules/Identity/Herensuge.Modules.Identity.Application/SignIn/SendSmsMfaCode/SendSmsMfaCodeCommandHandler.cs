using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.SendSmsMfaCode;

internal sealed class SendSmsMfaCodeCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<SendSmsMfaCodeCommand>
{
    public async Task<Result> Handle(SendSmsMfaCodeCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!user.HasVerifiedPhone)
        {
            return UserErrors.MfaMethodNotAvailable;
        }

        Result issued = await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.SmsMfa, user.PhoneNumber, cancellationToken);
        if (issued.IsFailure)
        {
            return issued.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
