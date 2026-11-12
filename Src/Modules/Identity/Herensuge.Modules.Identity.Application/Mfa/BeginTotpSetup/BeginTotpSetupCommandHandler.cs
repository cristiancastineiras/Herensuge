namespace Herensuge.Modules.Identity.Application.Mfa.BeginTotpSetup;

internal sealed class BeginTotpSetupCommandHandler(
    IUserRepository users,
    ITotpService totpService,
    IUnitOfWork unitOfWork) : ICommandHandler<BeginTotpSetupCommand, TotpSetupResponse>
{
    public async Task<Result<TotpSetupResponse>> Handle(BeginTotpSetupCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        TotpSetup setup = totpService.GenerateSecret(user.Email);
        Result started = user.BeginTotpSetup(setup.ProtectedSecret);
        if (started.IsFailure)
        {
            return started.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TotpSetupResponse(setup.SharedKey, setup.AuthenticatorUri);
    }
}
