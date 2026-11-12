using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.RequestEmailChange;

/// <summary>Sends a confirmation link to the new address; the email changes only when it is opened.</summary>
internal sealed class RequestEmailChangeCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestEmailChangeCommand>
{
    public async Task<Result> Handle(RequestEmailChangeCommand command, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.NewEmail);
        if (email.IsFailure)
        {
            return email.Error;
        }

        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (await users.ExistsByEmailAsync(email.Value.Value, cancellationToken))
        {
            return UserErrors.EmailAlreadyInUse;
        }

        Result issued = await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.EmailChange, email.Value.Value, cancellationToken);
        if (issued.IsFailure)
        {
            return issued.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
