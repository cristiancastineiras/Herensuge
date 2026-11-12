namespace Herensuge.Modules.Identity.Application.ExternalLogins.UnlinkExternalLogin;

internal sealed class UnlinkExternalLoginCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkExternalLoginCommand>
{
    public async Task<Result> Handle(UnlinkExternalLoginCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result unlinked = user.UnlinkExternalLogin(command.Provider);
        if (unlinked.IsFailure)
        {
            return unlinked.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
