namespace Herensuge.Modules.Identity.Application.Users.ResetPassword;

/// <summary>Sets the new password and, through PasswordChanged, revokes every open session.</summary>
internal sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository tokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        OneTimeToken? token = await tokens.GetByLinkHashAsync(
            OneTimeTokenPurpose.PasswordReset,
            OneTimeToken.ComputeLinkHash(command.Token, hasher),
            cancellationToken);
        if (token is null)
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        Result consumed = token.Consume(command.Token, hasher, now);
        User? user = consumed.IsSuccess ? await users.GetByIdAsync(token.UserId, cancellationToken) : null;
        if (user is null)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        user.ResetPassword(passwordHasher.Hash(command.NewPassword), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
