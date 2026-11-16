using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Groups;

public sealed record CreateGroupCommand(Guid TenantId, Guid ActorUserId, string Name, string? Description) : ICommand<Guid>;

public sealed record UpdateGroupCommand(Guid TenantId, Guid ActorUserId, Guid GroupId, string Name, string? Description) : ICommand;

public sealed record DeleteGroupCommand(Guid TenantId, Guid ActorUserId, Guid GroupId) : ICommand;

/// <summary>Replaces the members. Adding someone requires holding every permission of the group (plan D6, D12).</summary>
public sealed record SetGroupMembersCommand(Guid TenantId, Guid ActorUserId, Guid GroupId, IReadOnlyList<Guid> UserIds) : ICommand;

public sealed record GetGroupsQuery(Guid TenantId) : IQuery<IReadOnlyList<GroupResponse>>;

public sealed record GroupResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid[] MemberUserIds { get; init; } = [];

    /// <summary>Roles the group holds now (expired assignments excluded).</summary>
    public int Roles { get; init; }
}

internal sealed class GroupCommandHandlers(
    TenantAdministration administration,
    ITenancyModuleApi tenancy,
    IGroupRepository groups,
    IRoleAssignmentRepository assignments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) :
    ICommandHandler<CreateGroupCommand, Guid>,
    ICommandHandler<UpdateGroupCommand>,
    ICommandHandler<DeleteGroupCommand>,
    ICommandHandler<SetGroupMembersCommand>
{
    public async Task<Result<Guid>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await RequireAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        IReadOnlyList<Group> existing = await groups.ListAsync(command.TenantId, cancellationToken);
        if (existing.Count >= Group.MaxPerTenant)
        {
            return AccessErrors.TooManyGroups;
        }

        if (NameInUse(existing, command.Name, exceptId: null))
        {
            return AccessErrors.GroupNameInUse;
        }

        Result<Group> group = Group.Create(command.TenantId, command.Name, command.Description, timeProvider.GetUtcNow().UtcDateTime);
        if (group.IsFailure)
        {
            return group.Error;
        }

        groups.Add(group.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.Value.Id;
    }

    public async Task<Result> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        Result<Group> group = await FindAsync(command.TenantId, command.ActorUserId, command.GroupId, cancellationToken);
        if (group.IsFailure)
        {
            return group.Error;
        }

        if (NameInUse(await groups.ListAsync(command.TenantId, cancellationToken), command.Name, command.GroupId))
        {
            return AccessErrors.GroupNameInUse;
        }

        Result updated = group.Value.Update(command.Name, command.Description);
        if (updated.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        Result<Group> group = await FindAsync(command.TenantId, command.ActorUserId, command.GroupId, cancellationToken);
        if (group.IsFailure)
        {
            return group.Error;
        }

        foreach (RoleAssignment assignment in await assignments.ListAsync(command.TenantId, [command.GroupId], cancellationToken))
        {
            assignment.Revoke();
            assignments.Remove(assignment);
        }

        group.Value.Delete();
        groups.Remove(group.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(SetGroupMembersCommand command, CancellationToken cancellationToken)
    {
        Result<Group> found = await FindAsync(command.TenantId, command.ActorUserId, command.GroupId, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error;
        }

        Group group = found.Value;
        Guid[] added = [.. command.UserIds.Distinct().Except(group.MemberUserIds)];
        foreach (Guid userId in added)
        {
            if (await tenancy.GetMembershipAsync(command.TenantId, userId, cancellationToken) is null)
            {
                return AccessErrors.NotAMember;
            }
        }

        if (added.Length > 0)
        {
            Result canGrant = await administration.EnsureCanGrantGroupAsync(command.TenantId, command.ActorUserId, group.Id, cancellationToken);
            if (canGrant.IsFailure)
            {
                return canGrant;
            }
        }

        Result set = group.SetMembers(command.UserIds);
        if (set.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return set;
    }

    private static bool NameInUse(IReadOnlyList<Group> existing, string name, Guid? exceptId) =>
        existing.Any(group => group.Id != exceptId && string.Equals(group.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    private Task<Result> RequireAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken) =>
        administration.RequirePermissionAsync(tenantId, actorUserId, TenantAdministration.ManageRoles, cancellationToken);

    private async Task<Result<Group>> FindAsync(Guid tenantId, Guid actorUserId, Guid groupId, CancellationToken cancellationToken)
    {
        Result allowed = await RequireAsync(tenantId, actorUserId, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        Group? group = await groups.GetAsync(tenantId, groupId, cancellationToken);
        return group is null ? AccessErrors.GroupNotFound : group;
    }
}

internal sealed class GetGroupsQueryHandler(IDbConnectionFactory dbConnectionFactory, TimeProvider timeProvider)
    : IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupResponse>>
{
    private const string Sql =
        """
        SELECT g.id, g.name, g.description, g.member_user_ids,
               (SELECT count(*) FROM access.role_assignments a
                WHERE a.subject_id = g.id AND (a.expires_at_utc IS NULL OR a.expires_at_utc > @Now))::int AS roles
        FROM access.groups g
        WHERE g.tenant_id = @TenantId
        ORDER BY g.name
        """;

    public async Task<Result<IReadOnlyList<GroupResponse>>> Handle(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<GroupResponse> rows = await connection.QueryAsync<GroupResponse>(new CommandDefinition(
            Sql,
            new { query.TenantId, Now = timeProvider.GetUtcNow().UtcDateTime },
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<GroupResponse>>([.. rows]);
    }
}
