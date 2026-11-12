using FluentValidation;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.SignIn.SignInWithPassword;

internal sealed class SignInWithPasswordCommandValidator : AbstractValidator<SignInWithPasswordCommand>
{
    public SignInWithPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Email.MaxLength);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(PasswordRules.MaximumLength);
    }
}
