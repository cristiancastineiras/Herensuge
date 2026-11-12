using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.ResendEmailVerification;

/// <summary>Always succeeds: the caller cannot learn whether the account exists.</summary>
internal sealed class ResendEmailVerificationCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<ResendEmailVerificationCommand>
{
    public async Task<Result> Handle(ResendEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return Result.Success();
        }

        User? user = await users.GetByEmailAsync(email.Value.Value, cancellationToken);
        if (user is { IsEmailVerified: false })
        {
            Result issued = await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.EmailVerification, user.Email, cancellationToken);
            if (issued.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }
}
