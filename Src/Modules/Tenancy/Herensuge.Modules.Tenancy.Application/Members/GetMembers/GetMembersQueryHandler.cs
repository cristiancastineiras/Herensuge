using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Tenancy.Application.Members.GetMembers;

/// <summary>Memberships come from Tenancy; names and emails from Identity's public API.</summary>
internal sealed class GetMembersQueryHandler(IDbConnectionFactory dbConnectionFactory, IIdentityModuleApi identity)
    : IQueryHandler<GetMembersQuery, IReadOnlyList<MemberResponse>>
{
    private const string Sql =
        """
        SELECT user_id, lower(role) AS role, created_at_utc
        FROM tenancy.memberships
        WHERE tenant_id = @TenantId
        """;

    public async Task<Result<IReadOnlyList<MemberResponse>>> Handle(GetMembersQuery query, CancellationToken cancellationToken)
    {
        List<MembershipRow> memberships;
        await using (DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken))
        {
            memberships = [.. await connection.QueryAsync<MembershipRow>(
                new CommandDefinition(Sql, new { query.TenantId }, cancellationToken: cancellationToken))];
        }

        IReadOnlyList<UserSummary> users = await identity.GetUsersAsync([.. memberships.Select(m => m.UserId)], cancellationToken);
        Dictionary<Guid, UserSummary> byId = users.ToDictionary(user => user.Id);

        return Result.Success<IReadOnlyList<MemberResponse>>(
        [
            .. memberships
                .Where(membership => byId.ContainsKey(membership.UserId))
                .Select(membership => new MemberResponse(
                    membership.UserId,
                    byId[membership.UserId].Email,
                    byId[membership.UserId].DisplayName,
                    membership.Role,
                    membership.CreatedAtUtc))
                .OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        ]);
    }

    private sealed class MembershipRow
    {
        public Guid UserId { get; init; }

        public string Role { get; init; } = string.Empty;

        public DateTime CreatedAtUtc { get; init; }
    }
}
