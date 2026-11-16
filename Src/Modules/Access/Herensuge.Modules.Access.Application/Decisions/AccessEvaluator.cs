using System.Diagnostics;
using System.Diagnostics.Metrics;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Decisions;

/// <summary>A member of an organization asking about one application.</summary>
public sealed record AccessSubject(Guid TenantId, Guid UserId);

/// <summary>
/// What is known about the subject at decision time. <see cref="Assumed"/> (simulator): conditions are reported in
/// the trace, not evaluated.
/// </summary>
public sealed record SubjectFacts(
    bool SessionActive,
    IReadOnlyList<string> AuthenticationMethods,
    DateTimeOffset? AuthenticatedAt,
    string? Actor,
    bool Assumed = false)
{
    /// <summary>Herensuge's own checks: the access token was already validated (session included); no conditions apply.</summary>
    public static readonly SubjectFacts ValidatedSession = new(true, [], null, null);
}

/// <summary>What the subject holds in one application: role keys and every permission its roles grant (with or without condition).</summary>
public sealed record AccessGrants(string BaseRole, IReadOnlyList<string> Roles, IReadOnlySet<string> Permissions);

/// <summary>Claims of one application: unconditional permissions and the ones that need a condition (plan D3).</summary>
public sealed record AccessClaimSet(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, IReadOnlyList<string> ConditionalPermissions);

public sealed record EvaluationStep(string Name, bool Passed, string Detail);

/// <summary>Steps of one evaluation, in order (simulator).</summary>
public sealed class EvaluationTrace
{
    private readonly List<EvaluationStep> _steps = [];

    public IReadOnlyList<EvaluationStep> Steps => _steps;

    internal void Add(string name, bool passed, string detail) => _steps.Add(new EvaluationStep(name, passed, detail));
}

/// <summary>
/// The only place that decides (plan §5): the PDP, Herensuge's own checks, the token claims and the subset rule use it.
/// <see cref="ResolveGrantsAsync"/> reads the database; <see cref="Evaluate"/> is pure.
/// </summary>
// shortcut: no cache (a few indexed queries per decision); open a cache WP if the p99 of herensuge.access.decision.duration passes 25 ms.
public sealed class AccessEvaluator(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ITenancyModuleApi tenancy,
    TimeProvider timeProvider)
{
    // Same window as the RequireRecentAuthentication endpoint filter.
    private static readonly TimeSpan RecentAuthentication = TimeSpan.FromMinutes(10);

    private static readonly Meter Meter = new("Herensuge.Access");

    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        "herensuge.access.decisions",
        description: "Access decisions by application, result (permit, deny) and reason.");

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "herensuge.access.decision.duration",
        unit: "ms",
        description: "Time to resolve the grants and evaluate one access decision.");

    /// <summary>Null when the user is not a member of an active organization.</summary>
    public async Task<AccessGrants?> ResolveGrantsAsync(ResourceServer server, AccessSubject subject, CancellationToken cancellationToken)
    {
        MembershipSnapshot? membership = await tenancy.GetMembershipAsync(subject.TenantId, subject.UserId, cancellationToken);
        if (membership is not { TenantIsActive: true })
        {
            return null;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        RoleAssignment[] held = [.. (await assignments.ListAsync(subject.TenantId, subject.UserId, cancellationToken)).Where(assignment => assignment.IsActive(now))];

        RoleTemplate[] templates =
        [
            .. server.RoleTemplates.Where(template =>
                template.GrantTo.Contains(membership.Role, StringComparer.Ordinal) || held.Any(assignment => assignment.RoleTemplateId == template.Id)),
        ];

        Guid[] roleIds = [.. held.Where(assignment => assignment.RoleId is not null).Select(assignment => assignment.RoleId!.Value)];
        IReadOnlyList<Role> custom = roleIds.Length == 0 ? [] : await roles.GetManyAsync(subject.TenantId, roleIds, cancellationToken);

        Dictionary<Guid, string> active = server.ActivePermissions.ToDictionary(permission => permission.Id, permission => permission.Key);
        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        permissions.UnionWith(templates.SelectMany(template => template.PermissionKeys).Where(active.ContainsValue));
        permissions.UnionWith(custom.SelectMany(role => role.PermissionIds).Where(active.ContainsKey).Select(id => active[id]));

        return new AccessGrants(membership.Role, [.. templates.Select(template => template.Key)], permissions);
    }

    /// <summary>Plan §5 for one permission. <paramref name="grants"/> null = not a member of an active organization.</summary>
    public AccessDecision Evaluate(ResourceServer server, AccessGrants? grants, SubjectFacts facts, string permissionKey, EvaluationTrace? trace = null)
    {
        long started = Stopwatch.GetTimestamp();
        AccessDecision decision = EvaluateCore(server, grants, facts, permissionKey, trace);
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new KeyValuePair<string, object?>("resource_server", server.Key));
        Decisions.Add(
            1,
            new KeyValuePair<string, object?>("resource_server", server.Key),
            new KeyValuePair<string, object?>("decision", decision.Decision ? "permit" : "deny"),
            new KeyValuePair<string, object?>("reason", decision.Reason));
        return decision;
    }

    /// <summary>Plan D3: what goes in "permissions" and what in "conditional_permissions".</summary>
    public static AccessClaimSet ToClaims(ResourceServer server, AccessGrants grants)
    {
        Dictionary<string, Permission> active = server.ActivePermissions.ToDictionary(permission => permission.Key, StringComparer.Ordinal);
        var unconditional = new List<string>();
        var conditional = new List<string>();
        foreach (string key in grants.Permissions)
        {
            (active[key].Requires == PermissionRequirements.None ? unconditional : conditional).Add(key);
        }

        return new AccessClaimSet(grants.Roles, unconditional, conditional);
    }

    /// <summary>Plan D12: the permissions (keys) of <paramref name="requested"/> the actor does not hold. The owner holds all.</summary>
    public static IReadOnlyList<string> MissingToGrant(AccessGrants? actor, IEnumerable<string> requested) =>
        actor?.BaseRole == BaseRoles.Owner ? [] : [.. requested.Where(key => actor is null || !actor.Permissions.Contains(key)).Distinct(StringComparer.Ordinal)];

    private AccessDecision EvaluateCore(ResourceServer server, AccessGrants? grants, SubjectFacts facts, string permissionKey, EvaluationTrace? trace)
    {
        Permission? permission = server.ActivePermissions.FirstOrDefault(candidate => candidate.Key == permissionKey);
        trace?.Add("permission", permission is not null, permission is null ? $"«{permissionKey}» no existe o está retirado" : permissionKey);
        if (permission is null)
        {
            return Deny(AccessReasons.UnknownPermission);
        }

        trace?.Add("session", facts.SessionActive, facts.Assumed ? "supuesta activa" : facts.SessionActive ? "activa" : "cerrada o caducada");
        if (!facts.SessionActive)
        {
            return Deny(AccessReasons.SessionInactive);
        }

        trace?.Add("membership", grants is not null, grants is null ? "no es miembro de una organización activa" : $"rol base {grants.BaseRole}");
        if (grants is null)
        {
            return Deny(AccessReasons.NotMember);
        }

        bool granted = grants.Permissions.Contains(permissionKey);
        trace?.Add("grant", granted, granted ? $"roles: {string.Join(", ", grants.Roles)}" : "ningún rol lo concede");
        if (!granted)
        {
            return Deny(AccessReasons.NoRole);
        }

        if (permission.Requires != PermissionRequirements.None)
        {
            if (facts.Assumed)
            {
                trace?.Add("condition", true, $"requiere {permission.Requires} (no evaluada)");
                return new AccessDecision(true, AccessReasons.GrantedIfCondition);
            }

            bool met = facts.Actor is null && permission.Requires switch
            {
                PermissionRequirements.Mfa => facts.AuthenticationMethods.Contains(AuthenticationMethods.MultiFactor, StringComparer.Ordinal),
                PermissionRequirements.RecentAuthentication => facts.AuthenticatedAt is { } at && timeProvider.GetUtcNow() - at <= RecentAuthentication,
                _ => false,
            };
            trace?.Add("condition", met, $"requiere {permission.Requires}");
            if (!met)
            {
                return Deny(facts.Actor is null ? AccessReasons.StepUpRequired : AccessReasons.ConditionNotMet);
            }
        }

        return new AccessDecision(true, AccessReasons.Granted);
    }

    private static AccessDecision Deny(string reason) => new(false, reason);
}
