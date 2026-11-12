using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Tenancy.Application.Tenants.GetTenant;

internal sealed class GetTenantQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTenantQuery, TenantResponse>
{
    private const string Sql =
        """
        SELECT t.id, t.name, t.slug, t.is_active, t.require_mfa, t.created_at_utc,
               (SELECT count(*) FROM tenancy.memberships m WHERE m.tenant_id = t.id)::int AS member_count
        FROM tenancy.tenants t
        WHERE t.id = @TenantId
        """;

    public async Task<Result<TenantResponse>> Handle(GetTenantQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        TenantResponse? tenant = await connection.QuerySingleOrDefaultAsync<TenantResponse>(
            new CommandDefinition(Sql, new { query.TenantId }, cancellationToken: cancellationToken));
        return tenant is null ? TenantErrors.NotFound : tenant;
    }
}
