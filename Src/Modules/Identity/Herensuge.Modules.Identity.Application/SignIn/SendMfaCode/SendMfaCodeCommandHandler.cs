using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.SendMfaCode;

internal sealed class SendMfaCodeCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<SendMfaCodeCommand>
{
    public async Task<Result> Handle(SendMfaCodeCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        (OneTimeTokenPurpose purpose, string? target) = command.Method switch
        {
            MfaMethods.Sms => (OneTimeTokenPurpose.SmsMfa, user.PhoneNumber),
            MfaMethods.Email => (OneTimeTokenPurpose.EmailMfa, user.Email),
            _ => (default(OneTimeTokenPurpose), null),
        };

        if (target is null || !user.GetMfaMethods().Contains(command.Method))
        {
            return UserErrors.MfaMethodNotAvailable;
        }

        Result issued = await tokenIssuer.IssueAsync(user.Id, purpose, target, cancellationToken);
        if (issued.IsFailure)
        {
            return issued.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
