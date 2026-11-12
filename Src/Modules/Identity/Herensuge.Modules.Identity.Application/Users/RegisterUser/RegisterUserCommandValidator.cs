using FluentValidation;
using Herensuge.Modules.Identity.Application.Services;

namespace Herensuge.Modules.Identity.Application.Users.RegisterUser;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Email.MaxLength);
        RuleFor(command => command.Password).ValidPassword();
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
    }
}
