namespace Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;

internal sealed class RegenerateRecoveryCodesCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ISecretHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<RegenerateRecoveryCodesCommand, RecoveryCodesResponse>
{
    public async Task<Result<RecoveryCodesResponse>> Handle(RegenerateRecoveryCodesCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result<IReadOnlyList<string>> codes = user.RegenerateRecoveryCodes(hasher, timeProvider.GetUtcNow().UtcDateTime);
        if (codes.IsFailure)
        {
            return codes.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResponse(codes.Value);
    }
}
