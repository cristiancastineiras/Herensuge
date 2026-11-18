using Herensuge.Modules.Identity.Application.Services;
using Herensuge.Modules.Identity.Application.Users.ProfileFields;
using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Identity.Application.Users.RegisterUser;

/// <summary>Always answers the same way, whether the email exists or not (no account enumeration).</summary>
internal sealed class RegisterUserCommandHandler(
    IUserRepository users,
    OneTimeTokenIssuer tokenIssuer,
    IPasswordHasher passwordHasher,
    IOidcModuleApi oidc,
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

        // The application's login page: open or closed registration, and the extra data it asks for. Checked before
        // looking the email up, so the answer still says nothing about existing accounts.
        LoginSettings settings = command.ClientId is null
            ? LoginSettings.Default
            : await oidc.GetLoginSettingsAsync(command.ClientId, cancellationToken) ?? LoginSettings.Default;
        if (!settings.AllowRegistration)
        {
            return UserErrors.RegistrationDisabled;
        }

        Result<Dictionary<string, string>> profileFields = ProfileFieldValues.Validate(settings.ProfileFields, command.ProfileFields);
        if (profileFields.IsFailure)
        {
            return profileFields.Error;
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
        Result updated = user.UpdateProfileFields(profileFields.Value);
        if (updated.IsFailure)
        {
            return updated;
        }

        users.Add(user);
        await tokenIssuer.IssueAsync(user.Id, OneTimeTokenPurpose.EmailVerification, user.Email, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
