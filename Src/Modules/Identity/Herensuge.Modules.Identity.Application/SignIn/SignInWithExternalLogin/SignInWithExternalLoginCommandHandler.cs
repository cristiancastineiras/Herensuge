using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Application.Services;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithExternalLogin;

/// <summary>
/// Account linking rules (RESUMEN 5.3): the external account is identified by (provider, subject);
/// an existing local account with the same email is never linked automatically.
/// </summary>
internal sealed class SignInWithExternalLoginCommandHandler(
    IUserRepository users,
    SignInCompleter signInCompleter,
    IUnitOfWork unitOfWork,
    IOptions<IdentityModuleOptions> options,
    TimeProvider timeProvider) : ICommandHandler<SignInWithExternalLoginCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(SignInWithExternalLoginCommand command, CancellationToken cancellationToken) =>
        IdentityMetrics.TrackSignIn(AuthenticationMethods.Federated, await HandleCoreAsync(command, cancellationToken));

    private async Task<Result<SignInResult>> HandleCoreAsync(SignInWithExternalLoginCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        User? linkedUser = await users.GetByExternalLoginAsync(command.Provider, command.Subject, cancellationToken);

        if (command.LinkToUserId is { } linkToUserId)
        {
            return await LinkAsync(command, linkedUser, linkToUserId, now, cancellationToken);
        }

        User user;
        if (linkedUser is not null)
        {
            user = linkedUser;
        }
        else
        {
            Result<Email> email = Email.Create(command.Email);
            if (email.IsFailure)
            {
                return UserErrors.ExternalEmailMissing;
            }

            if (await users.ExistsByEmailAsync(email.Value.Value, cancellationToken))
            {
                return UserErrors.ExternalEmailInUse;
            }

            user = User.RegisterWithExternalLogin(email.Value, command.DisplayName, command.Provider, command.Subject, command.EmailVerified, now);
            if (command.EmailVerified && options.Value.IsBootstrapAdmin(user.Email))
            {
                user.GrantPlatformAdmin();
            }

            users.Add(user);
        }

        Result<SignInStep> step = user.SignInWithExternalLogin(now);
        if (step.IsFailure)
        {
            return step.Error;
        }

        if (step.Value == SignInStep.MfaRequired)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SignInResult.MfaRequired(user, [AuthenticationMethods.Federated]);
        }

        UserSession session = signInCompleter.Complete(user, [AuthenticationMethods.Federated]);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SignInResult.SignedIn(session);
    }

    private async Task<Result<SignInResult>> LinkAsync(
        SignInWithExternalLoginCommand command,
        User? linkedUser,
        Guid linkToUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (linkedUser is not null)
        {
            return linkedUser.Id == linkToUserId ? SignInResult.Linked(linkToUserId) : UserErrors.ExternalLoginInUse;
        }

        User? user = await users.GetByIdAsync(linkToUserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result linked = user.LinkExternalLogin(command.Provider, command.Subject, command.Email, now);
        if (linked.IsFailure)
        {
            return linked.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SignInResult.Linked(user.Id);
    }
}
