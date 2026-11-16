using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Access.Application.Catalog.GetAccessCatalog;

internal sealed class GetAccessCatalogQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetAccessCatalogQuery, IReadOnlyList<ApplicationCatalogResponse>>
{
    private const string Sql =
        """
        SELECT id, key, name FROM access.resource_servers ORDER BY name;

        SELECT id, resource_server_id, key, description, requires, feature_key
        FROM access.permissions
        WHERE deprecated_at_utc IS NULL
        ORDER BY key;

        SELECT id, resource_server_id, key, name, description, permission_keys, grant_to
        FROM access.role_templates
        ORDER BY name;
        """;

    public async Task<Result<IReadOnlyList<ApplicationCatalogResponse>>> Handle(GetAccessCatalogQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using SqlMapper.GridReader grid = await connection.QueryMultipleAsync(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        List<ServerRow> servers = [.. await grid.ReadAsync<ServerRow>()];
        ILookup<Guid, PermissionResponse> permissions = (await grid.ReadAsync<PermissionResponse>()).ToLookup(permission => permission.ResourceServerId);
        ILookup<Guid, RoleTemplateResponse> roles = (await grid.ReadAsync<RoleTemplateResponse>()).ToLookup(role => role.ResourceServerId);

        return Result.Success<IReadOnlyList<ApplicationCatalogResponse>>(
        [
            .. servers.Select(server => new ApplicationCatalogResponse(server.Key, server.Name, [.. permissions[server.Id]], [.. roles[server.Id]])),
        ]);
    }

    private sealed class ServerRow
    {
        public Guid Id { get; init; }

        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }
}
