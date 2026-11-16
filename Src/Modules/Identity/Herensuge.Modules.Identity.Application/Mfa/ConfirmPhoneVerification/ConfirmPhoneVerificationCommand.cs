using Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmPhoneVerification;

/// <param name="CurrentSessionId">Kept open when this turns MFA on; the other sessions are closed.</param>
public sealed record ConfirmPhoneVerificationCommand(Guid UserId, string Code, Guid? CurrentSessionId) : ICommand<RecoveryCodesResponse>;
