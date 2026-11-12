using FluentValidation;

namespace Herensuge.Modules.Identity.Application.Passkeys.AddPasskey;

internal sealed class AddPasskeyCommandValidator : AbstractValidator<AddPasskeyCommand>
{
    public AddPasskeyCommandValidator()
    {
        RuleFor(command => command.AttestationResponseJson).NotEmpty().MaximumLength(64 * 1024);
        RuleFor(command => command.Name).MaximumLength(PasskeyCredential.MaxNameLength);
    }
}
