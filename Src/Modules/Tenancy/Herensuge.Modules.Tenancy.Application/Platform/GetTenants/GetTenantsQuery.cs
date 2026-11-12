namespace Herensuge.Modules.Tenancy.Application.Platform.GetTenants;

/// <summary>Every organization, for platform administrators.</summary>
public sealed record GetTenantsQuery(string? Search, int Page, int PageSize) : IQuery<PagedResponse<PlatformTenantResponse>>;

public sealed record PagedResponse<TItem>(IReadOnlyList<TItem> Items, int Page, int PageSize, long TotalCount);

public sealed record PlatformTenantResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool RequireMfa { get; init; }

    public int MemberCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
