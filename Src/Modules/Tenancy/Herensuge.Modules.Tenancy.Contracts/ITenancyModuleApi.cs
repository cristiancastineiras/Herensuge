namespace Herensuge.Modules.Tenancy.Contracts;

/// <summary>Synchronous, in-process queries other modules may run against Tenancy.</summary>
public interface ITenancyModuleApi
{
    Task<IReadOnlyList<MembershipSnapshot>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    Task<MembershipSnapshot?> GetMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>True when any active organization of the user requires multi-factor authentication.</summary>
    Task<bool> IsMfaRequiredForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>True when the user is the only owner of at least one organization.</summary>
    Task<bool> IsLastOwnerOfAnyTenantAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record MembershipSnapshot(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Role,
    bool TenantIsActive,
    bool TenantRequiresMfa);

/// <summary>Values of the tenant_role claim.</summary>
public static class TenantRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
}
