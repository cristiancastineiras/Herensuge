using FluentValidation;
using Herensuge.Common.Application.Security;

namespace Herensuge.Modules.Tenancy.Application.Tenants.UpdateTenant;

internal sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MinimumLength(2).MaximumLength(Tenant.MaxNameLength);

        // Plan D9: public ranges only; a private, loopback or link-local range would trust a misconfigured proxy.
        RuleFor(command => command.TrustedNetworks)
            .Must(networks => networks is null || (networks.Count <= NetworkAddresses.MaxTrustedNetworks && networks.All(NetworkAddresses.IsPublicNetwork)))
            .WithMessage($"Trusted networks must be at most {NetworkAddresses.MaxTrustedNetworks} public CIDR ranges (no private, loopback or link-local ones).");
    }
}
