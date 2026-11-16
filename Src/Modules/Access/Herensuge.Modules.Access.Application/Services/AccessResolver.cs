using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Services;

/// <summary>Application roles (keys) and permissions a member holds in one application.</summary>
public sealed record EffectiveAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>
/// Effective access = application roles granted by the base role (owner/admin/member) + assignments not expired
/// (application roles and organization roles), limited to active permissions. Read from the database every time.
/// </summary>
// shortcut: no cache (a few indexed queries per decision); add the versioned cache of the plan (section 7) when the PDP shows up in traces.
public sealed class AccessResolver(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ITenancyModuleApi tenancy,
    TimeProvider timeProvider)
{
    /// <returns>Null when the user is not a member of an active organization.</returns>
    public async Task<EffectiveAccess?> ResolveAsync(ResourceServer server, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        MembershipSnapshot? membership = await tenancy.GetMembershipAsync(tenantId, userId, cancellationToken);
        if (membership is not { TenantIsActive: true })
        {
            return null;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        RoleAssignment[] held = [.. (await assignments.ListAsync(tenantId, userId, cancellationToken)).Where(assignment => assignment.IsActive(now))];

        RoleTemplate[] templates =
        [
            .. server.RoleTemplates.Where(template =>
                template.GrantTo.Contains(membership.Role, StringComparer.Ordinal) || held.Any(assignment => assignment.RoleTemplateId == template.Id)),
        ];

        Guid[] roleIds = [.. held.Where(assignment => assignment.RoleId is not null).Select(assignment => assignment.RoleId!.Value)];
        IReadOnlyList<Role> custom = roleIds.Length == 0 ? [] : await roles.GetManyAsync(tenantId, roleIds, cancellationToken);

        Dictionary<Guid, string> active = server.ActivePermissions.ToDictionary(permission => permission.Id, permission => permission.Key);
        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        permissions.UnionWith(templates.SelectMany(template => template.PermissionKeys).Where(active.ContainsValue));
        permissions.UnionWith(custom.SelectMany(role => role.PermissionIds).Where(active.ContainsKey).Select(id => active[id]));

        return new EffectiveAccess([.. templates.Select(template => template.Key)], [.. permissions]);
    }
}
