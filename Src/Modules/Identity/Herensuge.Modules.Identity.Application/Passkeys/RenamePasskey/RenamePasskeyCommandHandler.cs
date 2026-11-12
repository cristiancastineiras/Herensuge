namespace Herensuge.Modules.Identity.Application.Passkeys.RenamePasskey;

internal sealed class RenamePasskeyCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<RenamePasskeyCommand>
{
    public async Task<Result> Handle(RenamePasskeyCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result renamed = user.RenamePasskey(command.PasskeyId, command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
