using FluentValidation;

namespace Herensuge.Modules.Identity.Application.Passkeys.RenamePasskey;

internal sealed class RenamePasskeyCommandValidator : AbstractValidator<RenamePasskeyCommand>
{
    public RenamePasskeyCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(PasskeyCredential.MaxNameLength);
    }
}
