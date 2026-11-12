namespace Herensuge.Modules.Identity.Application.Passkeys.CreatePasskeyRegistrationOptions;

internal sealed class CreatePasskeyRegistrationOptionsCommandHandler(
    IUserRepository users,
    IPasskeyService passkeyService) : ICommandHandler<CreatePasskeyRegistrationOptionsCommand, string>
{
    public async Task<Result<string>> Handle(CreatePasskeyRegistrationOptionsCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.Passkeys.Count >= User.MaxPasskeys)
        {
            return UserErrors.PasskeyLimitReached;
        }

        return await passkeyService.CreateRegistrationOptionsAsync(
            new PasskeyUser(user.Id, user.Email, user.DisplayName),
            [.. user.Passkeys.Select(passkey => passkey.CredentialId)],
            cancellationToken);
    }
}
