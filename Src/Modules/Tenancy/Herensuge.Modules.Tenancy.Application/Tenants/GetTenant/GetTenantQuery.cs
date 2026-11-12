namespace Herensuge.Modules.Tenancy.Application.Tenants.GetTenant;

public sealed record GetTenantQuery(Guid TenantId) : IQuery<TenantResponse>;

public sealed record TenantResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool RequireMfa { get; init; }

    public int MemberCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
