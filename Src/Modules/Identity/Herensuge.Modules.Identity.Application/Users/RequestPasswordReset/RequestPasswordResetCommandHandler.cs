using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.RequestPasswordReset;

/// <summary>Always succeeds: the caller cannot learn whether the account exists.</summary>
internal sealed class RequestPasswordResetCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return Result.Success();
        }

        User? user = await users.GetByEmailAsync(email.Value.Value, cancellationToken);
        if (user is not null)
        {
            Result issued = await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.PasswordReset, user.Email, cancellationToken);
            if (issued.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }
}
