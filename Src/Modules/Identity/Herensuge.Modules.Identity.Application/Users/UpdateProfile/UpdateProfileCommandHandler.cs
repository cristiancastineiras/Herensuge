namespace Herensuge.Modules.Identity.Application.Users.UpdateProfile;

internal sealed class UpdateProfileCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProfileCommand>
{
    public async Task<Result> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        user.UpdateDisplayName(command.DisplayName);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
