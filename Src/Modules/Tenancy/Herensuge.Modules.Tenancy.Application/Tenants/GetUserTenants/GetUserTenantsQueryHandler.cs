using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Tenancy.Application.Tenants.GetUserTenants;

internal sealed class GetUserTenantsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUserTenantsQuery, IReadOnlyList<UserTenantResponse>>
{
    private const string Sql =
        """
        SELECT t.id, t.name, t.slug, lower(m.role) AS role, t.is_active, t.require_mfa
        FROM tenancy.memberships m
        JOIN tenancy.tenants t ON t.id = m.tenant_id
        WHERE m.user_id = @UserId
        ORDER BY t.name
        """;

    public async Task<Result<IReadOnlyList<UserTenantResponse>>> Handle(GetUserTenantsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<UserTenantResponse> tenants = await connection.QueryAsync<UserTenantResponse>(
            new CommandDefinition(Sql, new { query.UserId }, cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<UserTenantResponse>>([.. tenants]);
    }
}
