using Herensuge.Common.Application.Context;
using Herensuge.Modules.Identity.Application.SignIn;
using Herensuge.Modules.Identity.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Identity.Application.Support;

/// <summary>
/// A platform administrator (signed in with a passkey) asks for a read-only support session on a user's account
/// (plan D11). The link is valid 5 minutes and can only be redeemed with the same administrator's passkey.
/// </summary>
public sealed record StartSupportSessionCommand(Guid AdminUserId, string Email, string Reason) : ICommand<SupportTicketResponse>;

/// <param name="Token">Goes in the fragment of the link (/support#token): it never reaches a server log.</param>
public sealed record SupportTicketResponse(string Token, DateTime ExpiresAtUtc);

/// <summary>Redeems the link in a browser without a Herensuge session: a session of the user with amr ["imp"].</summary>
public sealed record RedeemSupportTicketCommand(string Token, string AssertionResponseJson) : ICommand<SupportSignInResult>;

/// <param name="ImpersonatorUserId">Goes in the IdP cookie and, as act, in every token of the session.</param>
public sealed record SupportSignInResult(SignInResult SignIn, Guid ImpersonatorUserId);

/// <summary>Ends the support sessions past their 30 minutes (events, BFF logout). Run by a job.</summary>
public sealed record EndExpiredSupportSessionsCommand : ICommand<int>;

internal sealed class SupportSessionCommandHandlers(
    IUserRepository users,
    IUserSessionRepository sessions,
    IOneTimeTokenRepository tokens,
    IPasskeyService passkeys,
    ISecretHasher hasher,
    IRequestContext requestContext,
    IEventBus eventBus,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) :
    ICommandHandler<StartSupportSessionCommand, SupportTicketResponse>,
    ICommandHandler<RedeemSupportTicketCommand, SupportSignInResult>,
    ICommandHandler<EndExpiredSupportSessionsCommand, int>
{
    private const int MaxReasonLength = 500;

    public async Task<Result<SupportTicketResponse>> Handle(StartSupportSessionCommand command, CancellationToken cancellationToken)
    {
        string reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > MaxReasonLength)
        {
            return SupportErrors.InvalidReason;
        }

        User? admin = await users.GetByIdAsync(command.AdminUserId, cancellationToken);
        User? user = await users.GetByEmailAsync(command.Email.Trim(), cancellationToken);
        if (admin is not { IsPlatformAdmin: true })
        {
            return SupportErrors.NotAllowedTarget;
        }

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.IsPlatformAdmin || user.Id == admin.Id)
        {
            return SupportErrors.NotAllowedTarget;
        }

        DateTime now = Now;
        foreach (OneTimeToken previous in await tokens.GetActiveAsync(user.Id, OneTimeTokenPurpose.SupportSession, now, cancellationToken))
        {
            previous.Invalidate(now);
        }

        (OneTimeToken token, string value) = OneTimeToken.Issue(user.Id, OneTimeTokenPurpose.SupportSession, admin.Id.ToString(), hasher, now);
        tokens.Add(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Audit and an email to the user: the link itself never leaves this response.
        await eventBus.PublishAsync(new SupportSessionRequestedIntegrationEvent(user.Id, admin.Id, reason), cancellationToken);
        return new SupportTicketResponse(value, token.ExpiresAtUtc);
    }

    public async Task<Result<SupportSignInResult>> Handle(RedeemSupportTicketCommand command, CancellationToken cancellationToken)
    {
        DateTime now = Now;
        OneTimeToken? ticket = await tokens.GetByLinkHashAsync(
            OneTimeTokenPurpose.SupportSession,
            OneTimeToken.ComputeLinkHash(command.Token ?? string.Empty, hasher),
            cancellationToken);
        if (ticket is null || !ticket.IsActive(now) || !Guid.TryParse(ticket.Target, out Guid adminId))
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        // The same administrator, still one, with one of their own passkeys: a stolen link is useless.
        User? admin = await users.GetByIdAsync(adminId, cancellationToken);
        Result<byte[]> credentialId = passkeys.ReadCredentialId(command.AssertionResponseJson);
        PasskeyCredential? passkey = credentialId.IsSuccess
            ? admin?.Passkeys.FirstOrDefault(candidate => candidate.CredentialId.AsSpan().SequenceEqual(credentialId.Value))
            : null;
        if (admin is not { IsPlatformAdmin: true } || passkey is null)
        {
            return UserErrors.InvalidCredentials;
        }

        Result<PasskeyAssertion> assertion = await passkeys.VerifyAssertionAsync(
            command.AssertionResponseJson,
            new StoredPasskey(passkey.CredentialId, passkey.PublicKey, passkey.SignCount, admin.Id),
            cancellationToken);
        if (assertion.IsFailure)
        {
            return UserErrors.InvalidCredentials;
        }

        Result recorded = admin.RecordPasskeySignIn(credentialId.Value, assertion.Value.SignCount, now);
        Result consumed = recorded.IsSuccess ? ticket.Consume(command.Token!, hasher, now) : recorded;
        if (consumed.IsFailure)
        {
            return consumed.Error;
        }

        UserSession session = UserSession.StartSupport(ticket.UserId, admin.Id, requestContext.IpAddress, requestContext.UserAgent, now);
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SupportSignInResult(SignInResult.SignedIn(session), admin.Id);
    }

    public async Task<Result<int>> Handle(EndExpiredSupportSessionsCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<UserSession> expired = await sessions.GetExpiredSupportAsync(Now, cancellationToken);
        foreach (UserSession session in expired)
        {
            session.Revoke(SessionRevocationReasons.SupportExpired, Now);
        }

        if (expired.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
