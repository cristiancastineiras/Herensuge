using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.UnitTests;

public sealed class AccessEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    private static readonly ResourceServer Facturas = ResourceServer.Register(
        new AccessManifest(
            "facturas",
            "Facturación",
            "facturas-app",
            null,
            [new ManifestPermission("invoice:read", null, null), new ManifestPermission("invoice:approve", null, PermissionRequirements.Mfa)],
            [new ManifestRole("finance", "Finanzas", null, ["invoice:*"], [BaseRoles.Owner])]),
        Now).Value;

    private static readonly AccessGrants Finance = new(BaseRoles.Admin, ["finance"], new SortedSet<string>(["invoice:read", "invoice:approve"], StringComparer.Ordinal));

    // Evaluate is pure: the repositories are only used by ResolveGrantsAsync.
    private readonly AccessEvaluator _evaluator = new(null!, null!, null!, TimeProvider.System);

    [Fact]
    public void Steps_deny_in_order_unknown_session_membership_and_role()
    {
        var active = new SubjectFacts(true, ["pwd"], DateTimeOffset.UtcNow, null);

        Assert.Equal(AccessReasons.UnknownPermission, _evaluator.Evaluate(Facturas, Finance, active, "invoice:delete").Reason);
        Assert.Equal(AccessReasons.SessionInactive, _evaluator.Evaluate(Facturas, Finance, active with { SessionActive = false }, "invoice:read").Reason);
        Assert.Equal(AccessReasons.NotMember, _evaluator.Evaluate(Facturas, null, active, "invoice:read").Reason);
        Assert.Equal(
            AccessReasons.NoRole,
            _evaluator.Evaluate(Facturas, Finance with { Permissions = new SortedSet<string>(StringComparer.Ordinal) }, active, "invoice:read").Reason);
        Assert.True(_evaluator.Evaluate(Facturas, Finance, active, "invoice:read").Decision);
    }

    [Fact]
    public void A_condition_needs_mfa_and_is_never_met_with_an_actor()
    {
        var password = new SubjectFacts(true, ["pwd"], DateTimeOffset.UtcNow, null);
        var mfa = password with { AuthenticationMethods = ["pwd", "otp", "mfa"] };

        Assert.Equal(AccessReasons.StepUpRequired, _evaluator.Evaluate(Facturas, Finance, password, "invoice:approve").Reason);
        Assert.True(_evaluator.Evaluate(Facturas, Finance, mfa, "invoice:approve").Decision);
        Assert.Equal(AccessReasons.ConditionNotMet, _evaluator.Evaluate(Facturas, Finance, mfa with { Actor = "support-admin" }, "invoice:approve").Reason);
    }

    [Fact]
    public void The_simulator_reports_conditions_without_evaluating_them()
    {
        var trace = new EvaluationTrace();
        var assumed = new SubjectFacts(true, [], null, null, Assumed: true);

        AccessDecision decision = _evaluator.Evaluate(Facturas, Finance, assumed, "invoice:approve", trace);

        Assert.True(decision.Decision);
        Assert.Equal(AccessReasons.GrantedIfCondition, decision.Reason);
        Assert.Equal(["permission", "session", "membership", "grant", "condition"], trace.Steps.Select(step => step.Name));
    }

    [Fact]
    public void Claims_split_unconditional_and_conditional_permissions()
    {
        AccessClaimSet claims = AccessEvaluator.ToClaims(Facturas, Finance);

        Assert.Equal(["invoice:read"], claims.Permissions);
        Assert.Equal(["invoice:approve"], claims.ConditionalPermissions);
        Assert.Equal(["finance"], claims.Roles);
    }

    [Fact]
    public void Only_held_permissions_can_be_granted_except_by_an_owner()
    {
        AccessGrants reader = Finance with { Permissions = new SortedSet<string>(["invoice:read"], StringComparer.Ordinal) };

        Assert.Equal(["invoice:approve"], AccessEvaluator.MissingToGrant(reader, ["invoice:read", "invoice:approve"]));
        Assert.Empty(AccessEvaluator.MissingToGrant(reader with { BaseRole = BaseRoles.Owner }, ["invoice:approve"]));
        Assert.Equal(["invoice:read"], AccessEvaluator.MissingToGrant(null, ["invoice:read"]));
    }

    [Fact]
    public void Syncing_the_same_manifest_again_changes_nothing()
    {
        ResourceServer server = ResourceServer.Register(
            new AccessManifest("crm", "CRM", "crm-app", null, [new ManifestPermission("lead:read", null, null), new ManifestPermission("lead:edit", null, null)], []),
            Now).Value;
        server.ClearDomainEvents();

        // Same content, different order.
        AccessManifest reordered = new("crm", "CRM", "crm-app", null, [new ManifestPermission("lead:edit", null, null), new ManifestPermission("lead:read", null, null)], []);
        Assert.True(server.ApplyManifest(reordered, Now.AddDays(1)).IsSuccess);

        Assert.Equal(1, server.ManifestVersion);
        Assert.Empty(server.GetDomainEvents());
    }
}
