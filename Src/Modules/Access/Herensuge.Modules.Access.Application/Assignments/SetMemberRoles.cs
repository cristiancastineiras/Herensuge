using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Assignments;

/// <summary>Replaces the roles a member holds. Only the differences are recorded (and audited).</summary>
public sealed record SetMemberRolesCommand(Guid TenantId, Guid ActorUserId, Guid UserId, IReadOnlyList<RoleGrant> Roles) : ICommand;

/// <summary>Exactly one of <see cref="RoleId"/> (organization role) or <see cref="RoleTemplateId"/> (application role).</summary>
public sealed record RoleGrant(Guid? RoleId, Guid? RoleTemplateId, DateTime? ExpiresAtUtc);

internal sealed class SetMemberRolesCommandHandler(
    TenantAdministration administration,
    ITenancyModuleApi tenancy,
    IRoleRepository roles,
    IResourceServerRepository servers,
    IRoleAssignmentRepository assignments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetMemberRolesCommand>
{
    public async Task<Result> Handle(SetMemberRolesCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequireAdministratorAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (await tenancy.GetMembershipAsync(command.TenantId, command.UserId, cancellationToken) is null)
        {
            return AccessErrors.NotAMember;
        }

        Result valid = await ValidateAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return valid;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<RoleAssignment> current = await assignments.ListAsync(command.TenantId, command.UserId, cancellationToken);

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
                command.TenantId, command.UserId, grant.RoleId, grant.RoleTemplateId, grant.ExpiresAtUtc, command.ActorUserId, now);
            if (granted.IsFailure)
            {
                return granted.Error;
            }

            assignments.Add(granted.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Organization roles must belong to this organization; application roles must exist.</summary>
    private async Task<Result> ValidateAsync(SetMemberRolesCommand command, CancellationToken cancellationToken)
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
