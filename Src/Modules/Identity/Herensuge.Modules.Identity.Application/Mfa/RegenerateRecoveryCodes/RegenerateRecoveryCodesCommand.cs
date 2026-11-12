namespace Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;

public sealed record RegenerateRecoveryCodesCommand(Guid UserId) : ICommand<RecoveryCodesResponse>;

public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
