using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Identity.Application.Mfa.RemovePhoneNumber;

internal sealed class RemovePhoneNumberCommandHandler(
    IUserRepository users,
    ITenancyModuleApi tenancy,
    IUnitOfWork unitOfWork) : ICommandHandler<RemovePhoneNumberCommand>
{
    public async Task<Result> Handle(RemovePhoneNumberCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        bool required = await tenancy.IsMfaRequiredForUserAsync(user.Id, cancellationToken);
        Result removed = user.RemovePhoneNumber(required);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
