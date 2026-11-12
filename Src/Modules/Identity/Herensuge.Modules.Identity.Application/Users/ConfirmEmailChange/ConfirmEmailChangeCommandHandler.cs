namespace Herensuge.Modules.Identity.Application.Users.ConfirmEmailChange;

internal sealed class ConfirmEmailChangeCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository tokens,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<ConfirmEmailChangeCommand>
{
    public async Task<Result> Handle(ConfirmEmailChangeCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        OneTimeToken? token = await tokens.GetByLinkHashAsync(
            OneTimeTokenPurpose.EmailChange,
            OneTimeToken.ComputeLinkHash(command.Token, hasher),
            cancellationToken);
        if (token is null)
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        Result consumed = token.Consume(command.Token, hasher, now);
        User? user = consumed.IsSuccess ? await users.GetByIdAsync(token.UserId, cancellationToken) : null;
        Result<Email> newEmail = Email.Create(token.Target);
        if (user is null || newEmail.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        if (await users.ExistsByEmailAsync(newEmail.Value.Value, cancellationToken))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return UserErrors.EmailAlreadyInUse;
        }

        user.ChangeEmail(newEmail.Value, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
