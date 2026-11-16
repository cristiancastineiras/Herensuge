using Herensuge.Modules.Access.Application.Assignments;
using Herensuge.Modules.Access.Application.Decisions;

namespace Herensuge.Modules.Access.Application.Services;

/// <summary>
/// Who may manage access in an organization, decided by Herensuge's own permissions (dogfooding, plan D1) against the
/// database, and what they may grant (plan D12: only what they hold; owners hold everything).
/// </summary>
public sealed class TenantAdministration(
    IResourceServerRepository servers,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    AccessEvaluator evaluator,
    TimeProvider timeProvider)
{
    public const string ManageRoles = "roles:manage";
    public const string ManageServiceAccounts = "service-accounts:manage";
    public const string ApproveAccessRequests = "access-requests:approve";

    public async Task<Result> RequirePermissionAsync(Guid tenantId, Guid actorUserId, string permission, CancellationToken cancellationToken)
    {
        ResourceServer? herensuge = await servers.GetByKeyAsync(ResourceServer.HerensugeKey, cancellationToken);
        if (herensuge is null)
        {
            return AccessErrors.NotAllowed;
        }

        AccessGrants? grants = await evaluator.ResolveGrantsAsync(herensuge, new AccessSubject(tenantId, actorUserId), cancellationToken);
        return evaluator.Evaluate(herensuge, grants, SubjectFacts.ValidatedSession, permission).Decision ? Result.Success() : AccessErrors.NotAllowed;
    }

    /// <summary>Plan D12 for role grants (organization roles and application roles).</summary>
    public async Task<Result> EnsureCanGrantAsync(Guid tenantId, Guid actorUserId, IReadOnlyCollection<RoleGrant> grants, CancellationToken cancellationToken)
    {
        Guid[] roleIds = [.. grants.Where(grant => grant.RoleId is not null).Select(grant => grant.RoleId!.Value).Distinct()];
        IReadOnlyList<Role> granted = roleIds.Length == 0 ? [] : await roles.GetManyAsync(tenantId, roleIds, cancellationToken);
        return await EnsureCanGrantAsync(
            tenantId,
            actorUserId,
            granted.SelectMany(role => role.PermissionIds),
            grants.Where(grant => grant.RoleTemplateId is not null).Select(grant => grant.RoleTemplateId!.Value),
            cancellationToken);
    }

    /// <summary>Plan D12 for joining a group: the actor must hold every permission the group's roles grant.</summary>
    public async Task<Result> EnsureCanGrantGroupAsync(Guid tenantId, Guid actorUserId, Guid groupId, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        RoleGrant[] grants =
        [
            .. (await assignments.ListAsync(tenantId, [groupId], cancellationToken))
                .Where(assignment => assignment.IsActive(now))
                .Select(assignment => new RoleGrant(assignment.RoleId, assignment.RoleTemplateId, assignment.ExpiresAtUtc)),
        ];
        return await EnsureCanGrantAsync(tenantId, actorUserId, grants, cancellationToken);
    }

    /// <summary>Fails with the permissions (app/key) of <paramref name="permissionIds"/> and templates the actor does not hold.</summary>
    public async Task<Result> EnsureCanGrantAsync(
        Guid tenantId,
        Guid actorUserId,
        IEnumerable<Guid> permissionIds,
        IEnumerable<Guid> roleTemplateIds,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> wantedPermissions = [.. permissionIds];
        HashSet<Guid> wantedTemplates = [.. roleTemplateIds];
        if (wantedPermissions.Count == 0 && wantedTemplates.Count == 0)
        {
            return Result.Success();
        }

        var missing = new List<string>();
        foreach (ResourceServer server in await servers.ListAsync(cancellationToken))
        {
            string[] requested =
            [
                .. server.ActivePermissions.Where(permission => wantedPermissions.Contains(permission.Id)).Select(permission => permission.Key),
                .. server.RoleTemplates.Where(template => wantedTemplates.Contains(template.Id)).SelectMany(template => template.PermissionKeys),
            ];
            if (requested.Length == 0)
            {
                continue;
            }

            AccessGrants? actor = await evaluator.ResolveGrantsAsync(server, new AccessSubject(tenantId, actorUserId), cancellationToken);
            missing.AddRange(AccessEvaluator.MissingToGrant(actor, requested).Select(key => $"{server.Key}/{key}"));
        }

        return missing.Count == 0 ? Result.Success() : AccessErrors.CannotGrant(missing);
    }
}
