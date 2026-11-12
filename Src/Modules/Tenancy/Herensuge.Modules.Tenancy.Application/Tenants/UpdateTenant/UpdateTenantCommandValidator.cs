using FluentValidation;

namespace Herensuge.Modules.Tenancy.Application.Tenants.UpdateTenant;

internal sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MinimumLength(2).MaximumLength(Tenant.MaxNameLength);
    }
}
