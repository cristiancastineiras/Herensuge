using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Assignments;

/// <summary>
/// Replaces the roles a subject holds: a member, a group or a service account (one of <see cref="SubjectTypes"/>).
/// Only the differences are recorded (and audited).
/// </summary>
public sealed record SetRolesCommand(Guid TenantId, Guid ActorUserId, string SubjectType, Guid SubjectId, IReadOnlyList<RoleGrant> Roles) : ICommand;

/// <summary>Exactly one of <see cref="RoleId"/> (organization role) or <see cref="RoleTemplateId"/> (application role).</summary>
public sealed record RoleGrant(Guid? RoleId, Guid? RoleTemplateId, DateTime? ExpiresAtUtc);

internal sealed class SetRolesCommandHandler(
    TenantAdministration administration,
    ITenancyModuleApi tenancy,
    IRoleRepository roles,
    IGroupRepository groups,
    IServiceAccountRepository serviceAccounts,
    IResourceServerRepository servers,
    IRoleAssignmentRepository assignments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetRolesCommand>
{
    public async Task<Result> Handle(SetRolesCommand command, CancellationToken cancellationToken)
    {
        string permission = command.SubjectType == SubjectTypes.Client ? TenantAdministration.ManageServiceAccounts : TenantAdministration.ManageRoles;
        Result allowed = await administration.RequirePermissionAsync(command.TenantId, command.ActorUserId, permission, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Result exists = await SubjectExistsAsync(command, cancellationToken);
        if (exists.IsFailure)
        {
            return exists;
        }

        Result valid = await ValidateAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return valid;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<RoleAssignment> current = await assignments.ListAsync(command.TenantId, [command.SubjectId], cancellationToken);

        // Plan D12: what is newly granted must be held by the actor.
        RoleGrant[] added = [.. command.Roles.Where(grant => !current.Any(held => held.RoleId == grant.RoleId && held.RoleTemplateId == grant.RoleTemplateId))];
        Result canGrant = await administration.EnsureCanGrantAsync(command.TenantId, command.ActorUserId, added, cancellationToken);
        if (canGrant.IsFailure)
        {
            return canGrant;
        }

        foreach (RoleAssignment assignment in current.Where(held =>
                     !command.Roles.Any(grant => held.IsSameGrant(grant.RoleId, grant.RoleTemplateId, grant.ExpiresAtUtc))))
        {
            assignment.Revoke();
            assignments.Remove(assignment);
        }

        foreach (RoleGrant grant in command.Roles.Distinct().Where(grant =>
                     !current.Any(held => held.IsSameGrant(grant.RoleId, grant.RoleTemplateId, grant.ExpiresAtUtc))))
        {
            Result<RoleAssignment> granted = RoleAssignment.Grant(
                command.TenantId, command.SubjectType, command.SubjectId, grant.RoleId, grant.RoleTemplateId, grant.ExpiresAtUtc, command.ActorUserId, now);
            if (granted.IsFailure)
            {
                return granted.Error;
            }

            assignments.Add(granted.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SubjectExistsAsync(SetRolesCommand command, CancellationToken cancellationToken) => command.SubjectType switch
    {
        SubjectTypes.User => await tenancy.GetMembershipAsync(command.TenantId, command.SubjectId, cancellationToken) is null
            ? AccessErrors.NotAMember
            : Result.Success(),
        SubjectTypes.Group => await groups.GetAsync(command.TenantId, command.SubjectId, cancellationToken) is null
            ? AccessErrors.GroupNotFound
            : Result.Success(),
        SubjectTypes.Client => await serviceAccounts.GetAsync(command.TenantId, command.SubjectId, cancellationToken) is null
            ? AccessErrors.ServiceAccountNotFound
            : Result.Success(),
        _ => AccessErrors.InvalidAssignment,
    };

    /// <summary>Organization roles must belong to this organization; application roles must exist.</summary>
    private async Task<Result> ValidateAsync(SetRolesCommand command, CancellationToken cancellationToken)
    {
        Guid[] roleIds = [.. command.Roles.Where(grant => grant.RoleId is not null).Select(grant => grant.RoleId!.Value).Distinct()];
        if (roleIds.Length > 0 && (await roles.GetManyAsync(command.TenantId, roleIds, cancellationToken)).Count != roleIds.Length)
        {
            return AccessErrors.RoleNotFound;
        }

        HashSet<Guid> templates = [.. (await servers.ListAsync(cancellationToken)).SelectMany(server => server.RoleTemplates).Select(template => template.Id)];
        return command.Roles.All(grant => grant.RoleTemplateId is not { } id || templates.Contains(id))
            ? Result.Success()
            : AccessErrors.RoleNotFound;
    }
}
