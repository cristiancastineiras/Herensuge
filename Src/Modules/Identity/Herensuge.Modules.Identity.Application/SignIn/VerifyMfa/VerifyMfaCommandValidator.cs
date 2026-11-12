using FluentValidation;

namespace Herensuge.Modules.Identity.Application.SignIn.VerifyMfa;

internal sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(command => command.Method).Must(method => method is MfaMethods.Totp or MfaMethods.Sms or MfaMethods.RecoveryCode);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(20);
    }
}
