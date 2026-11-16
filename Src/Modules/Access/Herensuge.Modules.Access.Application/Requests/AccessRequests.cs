using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Access.Application.Assignments;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.Requests;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Requests;

/// <summary>A member asks for a role for some hours (plan D13).</summary>
public sealed record RequestAccessCommand(Guid TenantId, Guid UserId, Guid? RoleId, Guid? RoleTemplateId, int Hours, string Reason) : ICommand<Guid>;

public sealed record ApproveAccessRequestCommand(Guid TenantId, Guid ActorUserId, Guid RequestId) : ICommand;

public sealed record DenyAccessRequestCommand(Guid TenantId, Guid ActorUserId, Guid RequestId) : ICommand;

/// <param name="UserId">Null: every request of the organization (approvers); otherwise only that member's.</param>
public sealed record GetAccessRequestsQuery(Guid TenantId, Guid? UserId) : IQuery<IReadOnlyList<AccessRequestResponse>>;

public sealed record AccessRequestResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string? RoleName { get; init; }

    public string? ApplicationName { get; init; }

    public int Hours { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>pending, approved, denied or expired.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public Guid? DecidedByUserId { get; init; }

    public DateTime? DecidedAtUtc { get; init; }
}

/// <summary>"My access": what the member holds in each application, and the roles they can ask for.</summary>
public sealed record GetMyAccessQuery(Guid TenantId, Guid UserId) : IQuery<MyAccessResponse>;

public sealed record MyAccessResponse(IReadOnlyList<MyApplicationAccess> Applications, IReadOnlyList<RequestableRole> Requestable);

public sealed record MyApplicationAccess(
    string Key,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> ConditionalPermissions);

/// <param name="RoleId">Organization role, or null for an application role (<see cref="RoleTemplateId"/>).</param>
public sealed record RequestableRole(Guid? RoleId, Guid? RoleTemplateId, string Name, string Group);

internal sealed class AccessRequestCommandHandlers(
    TenantAdministration administration,
    ITenancyModuleApi tenancy,
    IRoleRepository roles,
    IResourceServerRepository servers,
    IAccessRequestRepository requests,
    IRoleAssignmentRepository assignments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) :
    ICommandHandler<RequestAccessCommand, Guid>,
    ICommandHandler<ApproveAccessRequestCommand>,
    ICommandHandler<DenyAccessRequestCommand>
{
    public async Task<Result<Guid>> Handle(RequestAccessCommand command, CancellationToken cancellationToken)
    {
        if (await tenancy.GetMembershipAsync(command.TenantId, command.UserId, cancellationToken) is not { TenantIsActive: true })
        {
            return AccessErrors.NotAMember;
        }

        bool exists = command.RoleId is { } roleId
            ? await roles.GetAsync(command.TenantId, roleId, cancellationToken) is not null
            : (await servers.ListAsync(cancellationToken)).Any(server => server.RoleTemplates.Any(template => template.Id == command.RoleTemplateId));
        if (!exists)
        {
            return AccessErrors.RoleNotFound;
        }

        Result<AccessRequest> request = AccessRequest.Create(
            command.TenantId, command.UserId, command.RoleId, command.RoleTemplateId, command.Hours, command.Reason, Now);
        if (request.IsFailure)
        {
            return request.Error;
        }

        requests.Add(request.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.Value.Id;
    }

    /// <summary>Plan D12: the approver must hold every permission of the requested role.</summary>
    public async Task<Result> Handle(ApproveAccessRequestCommand command, CancellationToken cancellationToken)
    {
        Result<AccessRequest> found = await FindAsync(command.TenantId, command.ActorUserId, command.RequestId, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error;
        }

        AccessRequest request = found.Value;
        Result canGrant = await administration.EnsureCanGrantAsync(
            command.TenantId, command.ActorUserId, [new RoleGrant(request.RoleId, request.RoleTemplateId, null)], cancellationToken);
        if (canGrant.IsFailure)
        {
            return canGrant;
        }

        Result<DateTime> approved = request.Approve(command.ActorUserId, Now);
        if (approved.IsFailure)
        {
            return approved.Error;
        }

        Result<RoleAssignment> assignment = RoleAssignment.Grant(
            command.TenantId, SubjectTypes.User, request.UserId, request.RoleId, request.RoleTemplateId, approved.Value, command.ActorUserId, Now);
        if (assignment.IsFailure)
        {
            return assignment.Error;
        }

        assignments.Add(assignment.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(DenyAccessRequestCommand command, CancellationToken cancellationToken)
    {
        Result<AccessRequest> found = await FindAsync(command.TenantId, command.ActorUserId, command.RequestId, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error;
        }

        Result denied = found.Value.Deny(command.ActorUserId, Now);
        if (denied.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return denied;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<Result<AccessRequest>> FindAsync(Guid tenantId, Guid actorUserId, Guid requestId, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequirePermissionAsync(tenantId, actorUserId, TenantAdministration.ApproveAccessRequests, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        AccessRequest? request = await requests.GetAsync(tenantId, requestId, cancellationToken);
        return request is null ? AccessErrors.AccessRequestNotFound : request;
    }
}

internal sealed class AccessRequestQueryHandlers(
    IDbConnectionFactory dbConnectionFactory,
    IResourceServerRepository servers,
    IRoleRepository roles,
    AccessEvaluator evaluator,
    TimeProvider timeProvider) :
    IQueryHandler<GetAccessRequestsQuery, IReadOnlyList<AccessRequestResponse>>,
    IQueryHandler<GetMyAccessQuery, MyAccessResponse>
{
    // Pending first; a pending request older than a day reads as expired (plan D13).
    private const string Sql =
        """
        SELECT r.id, r.user_id, coalesce(role.name, t.name) AS role_name, s.name AS application_name, r.hours, r.reason,
               CASE WHEN r.status = 'pending' AND r.created_at_utc < @ExpiredBefore THEN 'expired' ELSE r.status END AS status,
               r.created_at_utc, r.decided_by_user_id, r.decided_at_utc
        FROM access.access_requests r
        LEFT JOIN access.roles role ON role.id = r.role_id
        LEFT JOIN access.role_templates t ON t.id = r.role_template_id
        LEFT JOIN access.resource_servers s ON s.id = t.resource_server_id
        WHERE r.tenant_id = @TenantId AND (@UserId::uuid IS NULL OR r.user_id = @UserId)
        ORDER BY (r.status = 'pending' AND r.created_at_utc >= @ExpiredBefore) DESC, r.created_at_utc DESC
        LIMIT 200
        """;

    public async Task<Result<IReadOnlyList<AccessRequestResponse>>> Handle(GetAccessRequestsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<AccessRequestResponse> rows = await connection.QueryAsync<AccessRequestResponse>(new CommandDefinition(
            Sql,
            new { query.TenantId, query.UserId, ExpiredBefore = timeProvider.GetUtcNow().UtcDateTime - AccessRequest.PendingLifetime },
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<AccessRequestResponse>>([.. rows]);
    }

    public async Task<Result<MyAccessResponse>> Handle(GetMyAccessQuery query, CancellationToken cancellationToken)
    {
        var applications = new List<MyApplicationAccess>();
        var requestable = new List<RequestableRole>();
        foreach (ResourceServer server in (await servers.ListAsync(cancellationToken)).OrderBy(server => server.Name, StringComparer.Ordinal))
        {
            requestable.AddRange(server.RoleTemplates.Select(template => new RequestableRole(null, template.Id, template.Name, server.Name)));
            if (await evaluator.ResolveGrantsAsync(server, new AccessSubject(query.TenantId, query.UserId), cancellationToken) is not { } grants)
            {
                return AccessErrors.NotAMember;
            }

            AccessClaimSet claims = AccessEvaluator.ToClaims(server, grants);
            if (claims.Roles.Count + claims.Permissions.Count + claims.ConditionalPermissions.Count > 0)
            {
                applications.Add(new MyApplicationAccess(server.Key, server.Name, claims.Roles, claims.Permissions, claims.ConditionalPermissions));
            }
        }

        requestable.AddRange((await roles.ListAsync(query.TenantId, cancellationToken))
            .OrderBy(role => role.Name, StringComparer.Ordinal)
            .Select(role => new RequestableRole(role.Id, null, role.Name, "Roles de la organización")));
        return new MyAccessResponse(applications, requestable);
    }
}
