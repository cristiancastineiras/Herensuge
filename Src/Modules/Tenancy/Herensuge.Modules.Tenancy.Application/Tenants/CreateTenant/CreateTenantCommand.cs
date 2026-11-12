namespace Herensuge.Modules.Tenancy.Application.Tenants.CreateTenant;

/// <summary>Onboarding: creates an organization with the user as its owner.</summary>
public sealed record CreateTenantCommand(Guid UserId, string Name) : ICommand<Guid>;
