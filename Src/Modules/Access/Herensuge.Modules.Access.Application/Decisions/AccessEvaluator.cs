using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Access.Application.Decisions;

/// <summary>A member of an organization asking about one application.</summary>
public sealed record AccessSubject(Guid TenantId, Guid UserId);

/// <summary>
/// What is known about the subject at decision time. <see cref="Assumed"/> (simulator): conditions are reported in
/// the trace, not evaluated. <see cref="IsClient"/>: a service account, which never meets a condition.
/// </summary>
public sealed record SubjectFacts(
    bool SessionActive,
    IReadOnlyList<string> AuthenticationMethods,
    DateTimeOffset? AuthenticatedAt,
    string? Actor,
    bool Assumed = false,
    IPAddress? IpAddress = null,
    bool IsClient = false)
{
    /// <summary>Herensuge's own checks: the access token was already validated (session included); no conditions apply.</summary>
    public static readonly SubjectFacts ValidatedSession = new(true, [], null, null);
}

/// <summary>
/// What the subject holds in one application: role keys and every permission its roles grant (with or without
/// condition). <see cref="Entitlements"/>: features of the app in the organization's plan, null when there are no
/// plans (everything included). <see cref="TrustedNetworks"/>: the organization's, for trusted_network.
/// </summary>
public sealed record AccessGrants(
    string BaseRole,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string>? Entitlements = null,
    IReadOnlyList<string>? TrustedNetworks = null);

/// <summary>Claims of one application: unconditional permissions, the ones that need a condition (plan D3) and features (D5).</summary>
public sealed record AccessClaimSet(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> ConditionalPermissions,
    IReadOnlyList<string> Entitlements);

public sealed record EvaluationStep(string Name, bool Passed, string Detail);

/// <summary>Steps of one evaluation, in order (simulator).</summary>
public sealed class EvaluationTrace
{
    private readonly List<EvaluationStep> _steps = [];

    public IReadOnlyList<EvaluationStep> Steps => _steps;

    internal void Add(string name, bool passed, string detail) => _steps.Add(new EvaluationStep(name, passed, detail));
}

/// <summary>
/// The only place that decides (plan §5): the PDP, Herensuge's own checks, the token claims, the simulator and the
/// subset rule use it. The Resolve methods read the database; <see cref="Evaluate"/> is pure.
/// </summary>
// shortcut: no cache (a few indexed queries per decision); open a cache WP if the p99 of herensuge.access.decision.duration passes 25 ms.
public sealed class AccessEvaluator(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IGroupRepository groups,
    IPlanRepository plans,
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

    /// <summary>A member: direct assignments, those of their groups and templates by base role. Null when not a member of an active organization.</summary>
    public async Task<AccessGrants?> ResolveGrantsAsync(ResourceServer server, AccessSubject subject, CancellationToken cancellationToken)
    {
        MembershipSnapshot? membership = await tenancy.GetMembershipAsync(subject.TenantId, subject.UserId, cancellationToken);
        if (membership is not { TenantIsActive: true })
        {
            return null;
        }

        IReadOnlyList<Group> memberOf = await groups.ListByMemberAsync(subject.TenantId, subject.UserId, cancellationToken);
        return await GrantsAsync(
            server,
            subject.TenantId,
            membership.Role,
            [subject.UserId, .. memberOf.Select(group => group.Id)],
            membership.TenantTrustedNetworks,
            cancellationToken);
    }

    /// <summary>A service account (plan D7): only its own assignments. Null when its organization is not active.</summary>
    public async Task<AccessGrants?> ResolveGrantsAsync(ResourceServer server, ServiceAccount account, CancellationToken cancellationToken) =>
        await tenancy.IsTenantActiveAsync(account.TenantId, cancellationToken)
            ? await GrantsAsync(server, account.TenantId, baseRole: string.Empty, [account.Id], [], cancellationToken)
            : null;

    /// <summary>
    /// Plan §5 for one permission. <paramref name="grants"/> null = not a member of an active organization.
    /// <paramref name="relation"/>: the relation check on the object, when <see cref="RelationNeeded"/> asked for one.
    /// </summary>
    public AccessDecision Evaluate(
        ResourceServer server,
        AccessGrants? grants,
        SubjectFacts facts,
        string permissionKey,
        EvaluationTrace? trace = null,
        RelationCheck? relation = null)
    {
        long started = Stopwatch.GetTimestamp();
        AccessDecision decision = EvaluateCore(server, grants, facts, permissionKey, trace, relation);
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new KeyValuePair<string, object?>("resource_server", server.Key));
        Decisions.Add(
            1,
            new KeyValuePair<string, object?>("resource_server", server.Key),
            new KeyValuePair<string, object?>("decision", decision.Decision ? "permit" : "deny"),
            new KeyValuePair<string, object?>("reason", decision.Reason));
        return decision;
    }

    /// <summary>Plan D3 and D5: what goes in "permissions", "conditional_permissions" and "entitlements".</summary>
    public static AccessClaimSet ToClaims(ResourceServer server, AccessGrants grants)
    {
        Dictionary<string, Permission> active = server.ActivePermissions.ToDictionary(permission => permission.Key, StringComparer.Ordinal);
        var unconditional = new List<string>();
        var conditional = new List<string>();
        foreach (string key in grants.Permissions.Where(key => IsEntitled(active[key], grants)))
        {
            Permission permission = active[key];
            (permission.Requires == PermissionRequirements.None && !permission.NeedsRelationWithRole ? unconditional : conditional).Add(key);
        }

        string[] entitlements = grants.Entitlements is { } entitled
            ? [.. entitled.Order(StringComparer.Ordinal)]
            : [.. server.Features.Select(feature => feature.Key).Order(StringComparer.Ordinal)];
        return new AccessClaimSet(grants.Roles, unconditional, conditional, entitlements);
    }

    /// <summary>Plan D12: the permissions (keys) of <paramref name="requested"/> the actor does not hold. The owner holds all.</summary>
    public static IReadOnlyList<string> MissingToGrant(AccessGrants? actor, IEnumerable<string> requested) =>
        actor?.BaseRole == BaseRoles.Owner ? [] : [.. requested.Where(key => actor is null || !actor.Permissions.Contains(key)).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The relation (owner, editor, viewer) to check on the object before <see cref="Evaluate"/>, or null when the
    /// decision does not depend on one: "or" with the role (the role suffices), "and" without it (denied anyway).
    /// </summary>
    public static string? RelationNeeded(ResourceServer server, AccessGrants? grants, string permissionKey)
    {
        Permission? permission = server.ActivePermissions.FirstOrDefault(candidate => candidate.Key == permissionKey);
        if (permission?.Relation is null || grants is null)
        {
            return null;
        }

        bool granted = grants.Permissions.Contains(permissionKey);
        return (permission.RelationMode == RelationModes.And) == granted ? permission.Relation : null;
    }

    private static bool IsEntitled(Permission permission, AccessGrants grants) =>
        permission.FeatureKey is null || grants.Entitlements is null || grants.Entitlements.Contains(permission.FeatureKey);

    private async Task<AccessGrants> GrantsAsync(
        ResourceServer server,
        Guid tenantId,
        string baseRole,
        IReadOnlyCollection<Guid> subjectIds,
        IReadOnlyList<string> trustedNetworks,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        RoleAssignment[] held = [.. (await assignments.ListAsync(tenantId, subjectIds, cancellationToken)).Where(assignment => assignment.IsActive(now))];

        RoleTemplate[] templates =
        [
            .. server.RoleTemplates.Where(template =>
                template.GrantTo.Contains(baseRole, StringComparer.Ordinal) || held.Any(assignment => assignment.RoleTemplateId == template.Id)),
        ];

        Guid[] roleIds = [.. held.Where(assignment => assignment.RoleId is not null).Select(assignment => assignment.RoleId!.Value).Distinct()];
        IReadOnlyList<Role> custom = roleIds.Length == 0 ? [] : await roles.GetManyAsync(tenantId, roleIds, cancellationToken);

        Dictionary<Guid, string> active = server.ActivePermissions.ToDictionary(permission => permission.Id, permission => permission.Key);
        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        permissions.UnionWith(templates.SelectMany(template => template.PermissionKeys).Where(active.ContainsValue));
        permissions.UnionWith(custom.SelectMany(role => role.PermissionIds).Where(active.ContainsKey).Select(id => active[id]));

        return new AccessGrants(
            baseRole,
            [.. templates.Select(template => template.Key)],
            permissions,
            await EntitlementsAsync(server, tenantId, cancellationToken),
            trustedNetworks);
    }

    /// <summary>Plan D5: features of <paramref name="server"/> in the organization's plan (or the default); null without plans.</summary>
    private async Task<IReadOnlySet<string>?> EntitlementsAsync(ResourceServer server, Guid tenantId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> all = await plans.ListAsync(cancellationToken);
        if (all.Count == 0)
        {
            return null;
        }

        TenantPlan? assigned = await plans.GetTenantPlanAsync(tenantId, cancellationToken);
        Plan? plan = all.FirstOrDefault(candidate => candidate.Id == assigned?.PlanId) ?? all.FirstOrDefault(candidate => candidate.IsDefault);
        string prefix = server.Key + "/";
        return plan is null
            ? null
            : new HashSet<string>(plan.Features.Where(feature => feature.StartsWith(prefix, StringComparison.Ordinal)).Select(feature => feature[prefix.Length..]), StringComparer.Ordinal);
    }

    private AccessDecision EvaluateCore(
        ResourceServer server,
        AccessGrants? grants,
        SubjectFacts facts,
        string permissionKey,
        EvaluationTrace? trace,
        RelationCheck? relation)
    {
        Permission? permission = server.ActivePermissions.FirstOrDefault(candidate => candidate.Key == permissionKey);
        trace?.Add("permission", permission is not null, permission is null ? $"«{permissionKey}» no existe o está retirado" : permissionKey);
        if (permission is null)
        {
            return Deny(AccessReasons.UnknownPermission);
        }

        trace?.Add("session", facts.SessionActive, facts.IsClient ? "cuenta de servicio" : facts.Assumed ? "supuesta activa" : facts.SessionActive ? "activa" : "cerrada o caducada");
        if (!facts.SessionActive)
        {
            return Deny(AccessReasons.SessionInactive);
        }

        trace?.Add("membership", grants is not null, grants is null ? "no pertenece a una organización activa" : facts.IsClient ? "de la organización" : $"rol base {grants.BaseRole}");
        if (grants is null)
        {
            return Deny(AccessReasons.NotMember);
        }

        bool entitled = IsEntitled(permission, grants);
        trace?.Add("feature", entitled, permission.FeatureKey is null ? "sin feature" : entitled ? $"«{permission.FeatureKey}» contratada" : $"«{permission.FeatureKey}» no está en el plan");
        if (!entitled)
        {
            return Deny(AccessReasons.NotEntitled);
        }

        // Step 5: role, relation on the object, or both (relationMode).
        bool granted = grants.Permissions.Contains(permissionKey);
        bool viaRelation = permission.Relation is not null && permission.RelationMode == RelationModes.Or && !granted;
        trace?.Add("grant", granted || viaRelation, granted ? $"roles: {string.Join(", ", grants.Roles)}" : viaRelation ? "sin rol: solo por relación" : "ningún rol lo concede");
        if (!granted && !viaRelation)
        {
            return Deny(AccessReasons.NoRole);
        }

        if (permission.Relation is not null && (viaRelation || permission.RelationMode == RelationModes.And))
        {
            if (facts.Assumed)
            {
                trace?.Add("relation", granted, $"requiere relación {permission.Relation} con cada objeto (no evaluada)");
                return granted ? new AccessDecision(true, AccessReasons.GrantedIfCondition) : Deny(AccessReasons.NoRole);
            }

            RelationCheck check = relation ?? RelationCheck.Missing;
            trace?.Add("relation", check.Holds, check.FailureReason ?? (check.Holds ? $"{permission.Relation} del objeto" : $"sin relación {permission.Relation}"));
            if (!check.Holds)
            {
                return Deny(check.FailureReason ?? AccessReasons.NoRelation);
            }
        }

        if (permission.Requires != PermissionRequirements.None)
        {
            if (facts.Assumed)
            {
                trace?.Add("condition", true, $"requiere {permission.Requires} (no evaluada)");
                return new AccessDecision(true, AccessReasons.GrantedIfCondition);
            }

            // A delegated token or a machine never meets a condition (plan §5, step 6).
            bool delegated = facts.Actor is not null || facts.IsClient;
            bool met = !delegated && permission.Requires switch
            {
                PermissionRequirements.Mfa => facts.AuthenticationMethods.Contains(AuthenticationMethods.MultiFactor, StringComparer.Ordinal),
                PermissionRequirements.RecentAuthentication => facts.AuthenticatedAt is { } at && timeProvider.GetUtcNow() - at <= RecentAuthentication,
                PermissionRequirements.TrustedNetwork => NetworkAddresses.IsTrusted(facts.IpAddress, grants.TrustedNetworks ?? []),
                _ => false,
            };
            trace?.Add("condition", met, $"requiere {permission.Requires}");
            if (!met)
            {
                return Deny(delegated
                    ? AccessReasons.ConditionNotMet
                    : permission.Requires == PermissionRequirements.TrustedNetwork ? AccessReasons.UntrustedNetwork : AccessReasons.StepUpRequired);
            }
        }

        return new AccessDecision(true, AccessReasons.Granted);
    }

    private static AccessDecision Deny(string reason) => new(false, reason);
}
