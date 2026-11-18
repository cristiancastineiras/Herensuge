using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Identity.Application.Users.ProfileFields;

/// <summary>
/// The data an application asks for, given by a signed-in user before entering it (/complete-profile): accounts
/// created through Google, Microsoft or another application of the SaaS never filled in its registration form.
/// </summary>
public sealed record UpdateProfileFieldsCommand(Guid UserId, string ClientId, IReadOnlyDictionary<string, string?> Values) : ICommand;

internal sealed class UpdateProfileFieldsCommandHandler(IUserRepository users, IOidcModuleApi oidc, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProfileFieldsCommand>
{
    public async Task<Result> Handle(UpdateProfileFieldsCommand command, CancellationToken cancellationToken)
    {
        LoginSettings settings = await oidc.GetLoginSettingsAsync(command.ClientId, cancellationToken) ?? LoginSettings.Default;
        Result<Dictionary<string, string>> values = ProfileFieldValues.Validate(settings.ProfileFields, command.Values);
        if (values.IsFailure)
        {
            return values.Error;
        }

        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result updated = user.UpdateProfileFields(values.Value);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
