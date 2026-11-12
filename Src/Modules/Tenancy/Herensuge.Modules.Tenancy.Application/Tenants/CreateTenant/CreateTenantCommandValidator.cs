using FluentValidation;

namespace Herensuge.Modules.Tenancy.Application.Tenants.CreateTenant;

internal sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MinimumLength(2).MaximumLength(Tenant.MaxNameLength);
    }
}
