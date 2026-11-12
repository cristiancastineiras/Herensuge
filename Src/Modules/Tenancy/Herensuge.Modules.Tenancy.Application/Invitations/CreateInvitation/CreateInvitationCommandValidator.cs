using FluentValidation;

namespace Herensuge.Modules.Tenancy.Application.Invitations.CreateInvitation;

internal sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(command => command.Role).Must(role => TenantRoleRules.TryParse(role, out _))
            .WithMessage("The role must be owner, admin or member.");
    }
}
