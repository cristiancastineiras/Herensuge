using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Tenancy.Infrastructure.PublicApi;

internal sealed class TenancyModuleApi(IDbConnectionFactory dbConnectionFactory) : ITenancyModuleApi
{
    private const string MembershipSql =
        """
        SELECT t.id AS tenant_id, t.name AS tenant_name, t.slug AS tenant_slug, lower(m.role) AS role,
               t.is_active AS tenant_is_active, t.require_mfa AS tenant_requires_mfa
        FROM tenancy.memberships m
        JOIN tenancy.tenants t ON t.id = m.tenant_id
        """;

    public async Task<IReadOnlyList<MembershipSnapshot>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<MembershipRow> rows = await connection.QueryAsync<MembershipRow>(new CommandDefinition(
            $"{MembershipSql} WHERE m.user_id = @UserId ORDER BY t.name",
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return [.. rows.Select(row => row.ToSnapshot())];
    }

    public async Task<MembershipSnapshot?> GetMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        MembershipRow? row = await connection.QuerySingleOrDefaultAsync<MembershipRow>(new CommandDefinition(
            $"{MembershipSql} WHERE m.user_id = @UserId AND m.tenant_id = @TenantId",
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return row?.ToSnapshot();
    }

    public async Task<bool> IsMfaRequiredForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM tenancy.memberships m
                JOIN tenancy.tenants t ON t.id = m.tenant_id
                WHERE m.user_id = @UserId AND t.is_active AND t.require_mfa)
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> IsLastOwnerOfAnyTenantAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM tenancy.memberships m
                WHERE m.user_id = @UserId AND m.role = 'Owner'
                  AND NOT EXISTS (
                      SELECT 1 FROM tenancy.memberships o
                      WHERE o.tenant_id = m.tenant_id AND o.role = 'Owner' AND o.user_id <> @UserId))
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    private sealed class MembershipRow
    {
        public Guid TenantId { get; init; }

        public string TenantName { get; init; } = string.Empty;

        public string TenantSlug { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public bool TenantIsActive { get; init; }

        public bool TenantRequiresMfa { get; init; }

        public MembershipSnapshot ToSnapshot() =>
            new(TenantId, TenantName, TenantSlug, Role, TenantIsActive, TenantRequiresMfa);
    }
}
