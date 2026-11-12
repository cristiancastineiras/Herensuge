namespace Herensuge.Modules.Tenancy.Application.Tenants.GetUserTenants;

public sealed record GetUserTenantsQuery(Guid UserId) : IQuery<IReadOnlyList<UserTenantResponse>>;

public sealed record UserTenantResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    /// <summary>owner, admin or member.</summary>
    public string Role { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool RequireMfa { get; init; }
}
