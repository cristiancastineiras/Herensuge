using Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmPhoneVerification;

public sealed record ConfirmPhoneVerificationCommand(Guid UserId, string Code) : ICommand<RecoveryCodesResponse>;
