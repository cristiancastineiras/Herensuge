using FluentValidation;

namespace Herensuge.Modules.Identity.Application.Users.UpdateProfile;

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
    }
}
