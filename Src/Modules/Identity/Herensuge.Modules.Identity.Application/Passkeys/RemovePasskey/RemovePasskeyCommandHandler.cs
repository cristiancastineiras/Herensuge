namespace Herensuge.Modules.Identity.Application.Passkeys.RemovePasskey;

internal sealed class RemovePasskeyCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<RemovePasskeyCommand>
{
    public async Task<Result> Handle(RemovePasskeyCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result removed = user.RemovePasskey(command.PasskeyId);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
