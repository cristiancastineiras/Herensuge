using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.RegisterUser;

/// <summary>Always answers the same way, whether the email exists or not (no account enumeration).</summary>
internal sealed class RegisterUserCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand>
{
    public async Task<Result> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return email.Error;
        }

        // Hashed before the lookup so the response time does not reveal whether the account exists.
        string passwordHash = passwordHasher.Hash(command.Password);

        User? existing = await users.GetByEmailAsync(email.Value.Value, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsEmailVerified)
            {
                Result issued = await tokenIssuer.IssueAsync(existing.Id, OneTimeTokenPurpose.EmailVerification, existing.Email, cancellationToken);
                if (issued.IsSuccess)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            return Result.Success();
        }

        var user = User.Register(email.Value, command.DisplayName, passwordHash, timeProvider.GetUtcNow().UtcDateTime);
        users.Add(user);
        await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.EmailVerification, user.Email, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
