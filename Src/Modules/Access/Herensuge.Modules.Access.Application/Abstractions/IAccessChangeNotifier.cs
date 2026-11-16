namespace Herensuge.Modules.Access.Application.Abstractions;

/// <summary>
/// Bumps the access versions the BFFs compare on every heartbeat (plan D4). Only a hint for the UI: decisions and
/// tokens never depend on it.
/// </summary>
public interface IAccessChangeNotifier
{
    /// <summary>Something that may affect every member changed (a role edited or deleted, a group, the plan).</summary>
    Task TenantChangedAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Something of one member changed (assignments, base role, removal).</summary>
    Task UserChangedAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
