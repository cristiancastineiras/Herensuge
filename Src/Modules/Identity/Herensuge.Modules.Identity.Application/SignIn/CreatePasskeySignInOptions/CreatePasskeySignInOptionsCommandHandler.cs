namespace Herensuge.Modules.Identity.Application.SignIn.CreatePasskeySignInOptions;

internal sealed class CreatePasskeySignInOptionsCommandHandler(IPasskeyService passkeyService)
    : ICommandHandler<CreatePasskeySignInOptionsCommand, string>
{
    public async Task<Result<string>> Handle(CreatePasskeySignInOptionsCommand command, CancellationToken cancellationToken) =>
        await passkeyService.CreateAssertionOptionsAsync(cancellationToken);
}
