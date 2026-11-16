using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Access.Application.ResourceServers.GetResourceServers;

internal sealed class GetResourceServersQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetResourceServersQuery, IReadOnlyList<ResourceServerResponse>>
{
    private const string Sql =
        """
        SELECT s.key, s.name, s.client_id, s.key || '.api' AS scope, s.key || '-api' AS audience,
               s.embed_permissions, s.manifest_version, s.updated_at_utc,
               (SELECT count(*) FROM access.permissions p WHERE p.resource_server_id = s.id AND p.deprecated_at_utc IS NULL)::int AS active_permissions,
               (SELECT count(*) FROM access.permissions p WHERE p.resource_server_id = s.id AND p.deprecated_at_utc IS NOT NULL)::int AS deprecated_permissions,
               (SELECT count(*) FROM access.role_templates t WHERE t.resource_server_id = s.id)::int AS role_templates
        FROM access.resource_servers s
        ORDER BY s.name
        """;

    public async Task<Result<IReadOnlyList<ResourceServerResponse>>> Handle(GetResourceServersQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<ResourceServerResponse> rows = await connection.QueryAsync<ResourceServerResponse>(
            new CommandDefinition(Sql, cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<ResourceServerResponse>>([.. rows]);
    }
}
