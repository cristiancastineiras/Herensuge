using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Identity.Application.Mfa.DisableTotp;

internal sealed class DisableTotpCommandHandler(
    IUserRepository users,
    ITenancyModuleApi tenancy,
    IUnitOfWork unitOfWork) : ICommandHandler<DisableTotpCommand>
{
    public async Task<Result> Handle(DisableTotpCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        bool required = await tenancy.IsMfaRequiredForUserAsync(user.Id, cancellationToken);
        Result disabled = user.DisableTotp(required);
        if (disabled.IsFailure)
        {
            return disabled.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
