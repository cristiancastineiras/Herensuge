namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmTotpSetup;

/// <param name="StepUpSessionId">Session to upgrade with otp+mfa (MFA enrolment during sign-in).</param>
public sealed record ConfirmTotpSetupCommand(Guid UserId, string Code, Guid? StepUpSessionId) : ICommand<ConfirmTotpSetupResponse>;

/// <param name="RecoveryCodes">Shown once; empty when the user already had codes.</param>
/// <param name="SessionMethods">amr of the upgraded session, when one was given.</param>
public sealed record ConfirmTotpSetupResponse(
    IReadOnlyList<string> RecoveryCodes,
    IReadOnlyList<string>? SessionMethods,
    DateTime? AuthenticatedAtUtc);
