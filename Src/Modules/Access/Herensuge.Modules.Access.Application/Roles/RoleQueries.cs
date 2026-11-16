using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Access.Application.Roles;

public sealed record GetRolesQuery(Guid TenantId) : IQuery<IReadOnlyList<RoleResponse>>;

public sealed record RoleResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid[] PermissionIds { get; init; } = [];

    /// <summary>Members, groups and service accounts holding it now (expired assignments excluded).</summary>
    public int Members { get; init; }
}

/// <summary>Assignments of a member, group or service account, expired ones included (the screen shows them as expired).</summary>
public sealed record GetSubjectRolesQuery(Guid TenantId, Guid SubjectId) : IQuery<IReadOnlyList<AssignedRoleResponse>>;

/// <summary>Either <see cref="RoleId"/> (organization role) or <see cref="RoleTemplateId"/> (application role).</summary>
public sealed record AssignedRoleResponse
{
    public Guid? RoleId { get; init; }

    public Guid? RoleTemplateId { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public DateTime GrantedAtUtc { get; init; }
}

internal sealed class GetRolesQueryHandler(IDbConnectionFactory dbConnectionFactory, TimeProvider timeProvider)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    private const string Sql =
        """
        SELECT r.id, r.name, r.description, r.permission_ids,
               (SELECT count(DISTINCT a.subject_id) FROM access.role_assignments a
                WHERE a.role_id = r.id AND (a.expires_at_utc IS NULL OR a.expires_at_utc > @Now))::int AS members
        FROM access.roles r
        WHERE r.tenant_id = @TenantId
        ORDER BY r.name
        """;

    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<RoleResponse> rows = await connection.QueryAsync<RoleResponse>(new CommandDefinition(
            Sql,
            new { query.TenantId, Now = timeProvider.GetUtcNow().UtcDateTime },
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<RoleResponse>>([.. rows]);
    }
}

internal sealed class GetSubjectRolesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetSubjectRolesQuery, IReadOnlyList<AssignedRoleResponse>>
{
    private const string Sql =
        """
        SELECT role_id, role_template_id, expires_at_utc, granted_at_utc
        FROM access.role_assignments
        WHERE tenant_id = @TenantId AND subject_id = @SubjectId
        ORDER BY granted_at_utc
        """;

    public async Task<Result<IReadOnlyList<AssignedRoleResponse>>> Handle(GetSubjectRolesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<AssignedRoleResponse> rows = await connection.QueryAsync<AssignedRoleResponse>(
            new CommandDefinition(Sql, new { query.TenantId, query.SubjectId }, cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<AssignedRoleResponse>>([.. rows]);
    }
}
