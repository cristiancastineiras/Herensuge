namespace Herensuge.Modules.Identity.Application.Passkeys.AddPasskey;

internal sealed class AddPasskeyCommandHandler(
    IUserRepository users,
    IPasskeyService passkeyService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AddPasskeyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddPasskeyCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result<NewPasskey> verified = await passkeyService.VerifyRegistrationAsync(user.Id, command.AttestationResponseJson, cancellationToken);
        if (verified.IsFailure)
        {
            return verified.Error;
        }

        NewPasskey passkey = verified.Value;
        Result<PasskeyCredential> added = user.AddPasskey(
            passkey.CredentialId,
            passkey.PublicKey,
            passkey.SignCount,
            passkey.Aaguid,
            command.Name,
            timeProvider.GetUtcNow().UtcDateTime);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return added.Value.Id;
    }
}
