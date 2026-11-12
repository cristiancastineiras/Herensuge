namespace Herensuge.Modules.Tenancy.Application.Platform.SetTenantStatus;

/// <summary>Platform admin activates or deactivates an organization. A deactivated one gets no new tokens.</summary>
public sealed record SetTenantStatusCommand(Guid TenantId, bool IsActive) : ICommand;
