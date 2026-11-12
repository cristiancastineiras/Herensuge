namespace Herensuge.Modules.Identity.Application.Users.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.PasswordHash is not null
            && (string.IsNullOrEmpty(command.CurrentPassword)
                || passwordHasher.Verify(user.PasswordHash, command.CurrentPassword) == PasswordVerificationResult.Failed))
        {
            return UserErrors.InvalidCurrentPassword;
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), command.CurrentSessionId, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
