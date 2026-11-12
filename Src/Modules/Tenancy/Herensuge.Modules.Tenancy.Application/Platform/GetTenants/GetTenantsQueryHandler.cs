using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Tenancy.Application.Platform.GetTenants;

internal sealed class GetTenantsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTenantsQuery, PagedResponse<PlatformTenantResponse>>
{
    private const string Sql =
        """
        SELECT t.id, t.name, t.slug, t.is_active, t.require_mfa, t.created_at_utc,
               (SELECT count(*) FROM tenancy.memberships m WHERE m.tenant_id = t.id)::int AS member_count,
               count(*) OVER () AS total_count
        FROM tenancy.tenants t
        WHERE @Pattern::text IS NULL OR t.name ILIKE @Pattern OR t.slug ILIKE @Pattern
        ORDER BY t.created_at_utc DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<Result<PagedResponse<PlatformTenantResponse>>> Handle(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        string? pattern = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : $"%{query.Search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        List<Row> rows = [.. await connection.QueryAsync<Row>(new CommandDefinition(
            Sql,
            new { Pattern = pattern, query.PageSize, Offset = (query.Page - 1) * query.PageSize },
            cancellationToken: cancellationToken))];

        long total = rows.Count > 0 ? rows[0].TotalCount : 0;
        return new PagedResponse<PlatformTenantResponse>(
            [.. rows.Select(row => new PlatformTenantResponse
            {
                Id = row.Id,
                Name = row.Name,
                Slug = row.Slug,
                IsActive = row.IsActive,
                RequireMfa = row.RequireMfa,
                MemberCount = row.MemberCount,
                CreatedAtUtc = row.CreatedAtUtc,
            })],
            query.Page,
            query.PageSize,
            total);
    }

    private sealed class Row
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Slug { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public bool RequireMfa { get; init; }

        public int MemberCount { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public long TotalCount { get; init; }
    }
}
