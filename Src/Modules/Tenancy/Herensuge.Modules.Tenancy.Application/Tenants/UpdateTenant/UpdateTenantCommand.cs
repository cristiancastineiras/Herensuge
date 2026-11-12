namespace Herensuge.Modules.Tenancy.Application.Tenants.UpdateTenant;

public sealed record UpdateTenantCommand(Guid TenantId, Guid ActorUserId, string Name, bool RequireMfa) : ICommand;
