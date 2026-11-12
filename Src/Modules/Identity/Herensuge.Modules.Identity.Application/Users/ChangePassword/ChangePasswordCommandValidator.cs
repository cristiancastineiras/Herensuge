using FluentValidation;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.ChangePassword;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).MaximumLength(PasswordRules.MaximumLength);
        RuleFor(command => command.NewPassword).ValidPassword();
    }
}
