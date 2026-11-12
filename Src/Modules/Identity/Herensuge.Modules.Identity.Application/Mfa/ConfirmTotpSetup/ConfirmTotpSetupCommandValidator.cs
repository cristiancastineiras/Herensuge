using FluentValidation;

namespace Herensuge.Modules.Identity.Application.Mfa.ConfirmTotpSetup;

internal sealed class ConfirmTotpSetupCommandValidator : AbstractValidator<ConfirmTotpSetupCommand>
{
    public ConfirmTotpSetupCommandValidator()
    {
        RuleFor(command => command.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$");
    }
}
