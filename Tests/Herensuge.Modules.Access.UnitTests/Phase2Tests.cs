using System.Net;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.UnitTests;

/// <summary>Plans and features (D5), groups (D6), service accounts (D7) and trusted networks (D9).</summary>
public sealed class Phase2Tests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    private static readonly ResourceServer Facturas = ResourceServer.Register(
        new AccessManifest(
            "facturas",
            "Facturación",
            "facturas-app",
            null,
            [
                new ManifestPermission("invoice:read", null, null),
                new ManifestPermission("invoice:approve", null, null, Feature: "approvals"),
                new ManifestPermission("invoice:export", null, PermissionRequirements.TrustedNetwork),
            ],
            [new ManifestRole("finance", "Finanzas", null, ["invoice:*"], [BaseRoles.Owner])],
            [new ManifestFeature("approvals", "Aprobaciones")]),
        Now).Value;

    private static readonly AccessGrants Finance = new(
        BaseRoles.Admin,
        ["finance"],
        new SortedSet<string>(["invoice:read", "invoice:approve", "invoice:export"], StringComparer.Ordinal));

    private static readonly SubjectFacts Signed = new(true, ["pwd"], DateTimeOffset.UtcNow, null);

    private readonly AccessEvaluator _evaluator = new(null!, null!, null!, null!, null!, TimeProvider.System);

    [Fact]
    public void A_feature_outside_the_plan_is_denied_and_left_out_of_the_claims()
    {
        AccessGrants basic = Finance with { Entitlements = new HashSet<string>(StringComparer.Ordinal) };

        Assert.Equal(AccessReasons.NotEntitled, _evaluator.Evaluate(Facturas, basic, Signed, "invoice:approve").Reason);
        Assert.True(_evaluator.Evaluate(Facturas, basic, Signed, "invoice:read").Decision);
        Assert.DoesNotContain("invoice:approve", AccessEvaluator.ToClaims(Facturas, basic).Permissions);
        Assert.Empty(AccessEvaluator.ToClaims(Facturas, basic).Entitlements);

        // Without plans (null), everything is included.
        Assert.True(_evaluator.Evaluate(Facturas, Finance, Signed, "invoice:approve").Decision);
        Assert.Equal(["approvals"], AccessEvaluator.ToClaims(Facturas, Finance).Entitlements);
    }

    [Fact]
    public void Trusted_network_needs_a_public_ip_inside_the_organizations_ranges()
    {
        AccessGrants office = Finance with { TrustedNetworks = ["203.0.113.0/24"] };

        Assert.True(_evaluator.Evaluate(Facturas, office, Signed with { IpAddress = IPAddress.Parse("203.0.113.9") }, "invoice:export").Decision);
        Assert.Equal(
            AccessReasons.UntrustedNetwork,
            _evaluator.Evaluate(Facturas, office, Signed with { IpAddress = IPAddress.Parse("198.51.100.1") }, "invoice:export").Reason);

        // A private address means a misconfigured proxy: denied even without a list.
        Assert.Equal(
            AccessReasons.UntrustedNetwork,
            _evaluator.Evaluate(Facturas, Finance, Signed with { IpAddress = IPAddress.Parse("10.0.0.7") }, "invoice:export").Reason);
        Assert.True(_evaluator.Evaluate(Facturas, Finance, Signed with { IpAddress = IPAddress.Parse("198.51.100.1") }, "invoice:export").Decision);
    }

    [Fact]
    public void A_service_account_never_meets_a_condition()
    {
        var machine = new SubjectFacts(true, [], null, null, IpAddress: IPAddress.Parse("203.0.113.9"), IsClient: true);

        Assert.True(_evaluator.Evaluate(Facturas, Finance, machine, "invoice:read").Decision);
        Assert.Equal(AccessReasons.ConditionNotMet, _evaluator.Evaluate(Facturas, Finance, machine, "invoice:export").Reason);
    }

    [Theory]
    [InlineData("203.0.113.0/24", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("8.8.8.8", true)]
    [InlineData("10.1.0.0/16", false)]
    [InlineData("192.168.1.0/24", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("fe80::/64", false)]
    [InlineData("0.0.0.0/0", false)]
    [InlineData("203.0.113.7/24", false)]
    [InlineData("not-a-network", false)]
    public void Only_public_ranges_can_be_trusted(string network, bool accepted) =>
        Assert.Equal(accepted, NetworkAddresses.IsPublicNetwork(network));

    [Fact]
    public void Group_membership_changes_raise_one_event_with_the_differences()
    {
        Group group = Group.Create(Guid.NewGuid(), "Contabilidad", null, Now).Value;
        Guid ana = Guid.NewGuid();
        Guid luis = Guid.NewGuid();
        group.ClearDomainEvents();

        Assert.True(group.SetMembers([ana, luis, ana]).IsSuccess);
        Assert.True(group.SetMembers([ana, luis]).IsSuccess);
        group.RemoveMember(luis);

        GroupMembersChangedDomainEvent[] changes = [.. group.GetDomainEvents().OfType<GroupMembersChangedDomainEvent>()];
        Assert.Equal(2, changes.Length);
        Assert.Equal([ana, luis], changes[0].Added);
        Assert.Equal([luis], changes[1].Removed);
        Assert.Equal([ana], group.MemberUserIds);
    }

    [Fact]
    public void Plans_hold_app_features_with_a_valid_key()
    {
        Plan plan = Plan.Create("pro", "Pro", ["facturas/approvals", "facturas/approvals"], isDefault: true, Now).Value;

        Assert.True(plan.Includes("facturas", "approvals"));
        Assert.Equal(["facturas/approvals"], plan.Features);
        Assert.Equal("Access.InvalidPlan", Plan.Create("Pro Plan", "Pro", [], false, Now).Error.Code);
        Assert.Equal("Access.InvalidPlan", Plan.Create("pro", "Pro", ["approvals"], false, Now).Error.Code);
    }

    [Fact]
    public void A_permission_can_only_use_a_declared_feature()
    {
        Result<ResourceServer> invalid = ResourceServer.Register(
            new AccessManifest("crm", "CRM", "crm-app", null, [new ManifestPermission("lead:read", null, null, Feature: "missing")], []),
            Now);

        Assert.True(invalid.IsFailure);
        Assert.Equal("Access.InvalidManifest", invalid.Error.Code);
    }
}
