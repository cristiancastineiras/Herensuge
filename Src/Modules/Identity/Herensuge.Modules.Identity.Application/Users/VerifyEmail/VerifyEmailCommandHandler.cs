using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Application.Users.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IOptions<IdentityModuleOptions> options,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<VerifyEmailCommand>
{
    public async Task<Result> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        OneTimeToken? token = await tokens.GetByLinkHashAsync(
            OneTimeTokenPurpose.EmailVerification,
            OneTimeToken.ComputeLinkHash(command.Token, hasher),
            cancellationToken);
        if (token is null)
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        Result consumed = token.Consume(command.Token, hasher, now);
        if (consumed.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return consumed.Error;
        }

        // The link is only valid for the address it was sent to.
        User? user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !string.Equals(user.Email, token.Target, StringComparison.Ordinal))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        user.VerifyEmail(now);
        if (options.Value.IsBootstrapAdmin(user.Email))
        {
            user.GrantPlatformAdmin();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
