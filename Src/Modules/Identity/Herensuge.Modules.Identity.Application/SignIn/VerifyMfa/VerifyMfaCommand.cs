namespace Herensuge.Modules.Identity.Application.SignIn.VerifyMfa;

/// <param name="Method">totp, sms or recovery_code.</param>
/// <param name="FirstFactorMethods">amr of the factor already verified (pwd or fed).</param>
/// <param name="StepUpSessionId">Existing session to upgrade instead of opening a new one.</param>
public sealed record VerifyMfaCommand(
    Guid UserId,
    string Method,
    string Code,
    IReadOnlyList<string> FirstFactorMethods,
    Guid? StepUpSessionId) : ICommand<SignInResult>;
