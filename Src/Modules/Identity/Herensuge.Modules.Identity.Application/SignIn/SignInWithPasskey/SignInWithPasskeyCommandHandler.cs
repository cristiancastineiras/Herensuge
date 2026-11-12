using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithPasskey;

/// <summary>A passkey with user verification is phishing-resistant multi-factor authentication on its own.</summary>
internal sealed class SignInWithPasskeyCommandHandler(
    IUserRepository users,
    IPasskeyService passkeyService,
    SignInCompleter signInCompleter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SignInWithPasskeyCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(SignInWithPasskeyCommand command, CancellationToken cancellationToken) =>
        IdentityMetrics.TrackSignIn(AuthenticationMethods.Passkey, await HandleCoreAsync(command, cancellationToken));

    private async Task<Result<SignInResult>> HandleCoreAsync(SignInWithPasskeyCommand command, CancellationToken cancellationToken)
    {
        Result<byte[]> credentialId = passkeyService.ReadCredentialId(command.AssertionResponseJson);
        if (credentialId.IsFailure)
        {
            return UserErrors.InvalidCredentials;
        }

        User? user = await users.GetByPasskeyCredentialIdAsync(credentialId.Value, cancellationToken);
        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.IsLockedOut(now))
        {
            return UserErrors.LockedOut;
        }

        PasskeyCredential stored = user.Passkeys.First(passkey => passkey.CredentialId.AsSpan().SequenceEqual(credentialId.Value));
        Result<PasskeyAssertion> assertion = await passkeyService.VerifyAssertionAsync(
            command.AssertionResponseJson,
            new StoredPasskey(stored.CredentialId, stored.PublicKey, stored.SignCount, user.Id),
            cancellationToken);

        if (assertion.IsFailure)
        {
            user.RegisterFailedPasskey(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return UserErrors.InvalidCredentials;
        }

        Result recorded = user.RecordPasskeySignIn(credentialId.Value, assertion.Value.SignCount, now);
        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        UserSession session = signInCompleter.Complete(user, [AuthenticationMethods.Passkey, AuthenticationMethods.MultiFactor]);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SignInResult.SignedIn(session);
    }
}
