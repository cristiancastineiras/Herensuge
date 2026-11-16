using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.ResourceServers.Events;
using Herensuge.Modules.Access.Domain.Roles;

namespace Herensuge.Modules.Access.UnitTests;

public sealed class AccessTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Registering_a_manifest_derives_scope_and_audience_and_expands_wildcards()
    {
        ResourceServer server = ResourceServer.Register(Manifest(), Now).Value;

        Assert.Equal("facturas.api", server.Scope);
        Assert.Equal("facturas-api", server.Audience);
        Assert.True(server.EmbedPermissions);
        Assert.Equal(["invoice:approve", "invoice:read"], server.RoleTemplates.Single(role => role.Key == "finance").PermissionKeys.Order());
        Assert.Equal([BaseRoles.Owner], server.RoleTemplates.Single(role => role.Key == "finance").GrantTo);
        Assert.Single(server.GetDomainEvents().OfType<ResourceServerSyncedDomainEvent>());
    }

    [Fact]
    public void A_permission_left_out_of_the_manifest_is_retired_and_comes_back_with_the_same_id()
    {
        ResourceServer server = ResourceServer.Register(Manifest(), Now).Value;
        Guid approveId = server.Permissions.Single(permission => permission.Key == "invoice:approve").Id;

        AccessManifest withoutApprove = Manifest() with
        {
            Permissions = [new ManifestPermission("invoice:read", "Ver", null)],
            Roles = [new ManifestRole("finance", "Finanzas", null, ["invoice:*"], [BaseRoles.Owner])],
        };
        Assert.True(server.ApplyManifest(withoutApprove, Now.AddDays(1)).IsSuccess);

        Permission approve = server.Permissions.Single(permission => permission.Key == "invoice:approve");
        Assert.False(approve.IsActive);
        Assert.Equal(["invoice:read"], server.RoleTemplates.Single().PermissionKeys);
        Assert.Equal(2, server.ManifestVersion);

        Assert.True(server.ApplyManifest(Manifest(), Now.AddDays(2)).IsSuccess);
        Assert.True(server.Permissions.Single(permission => permission.Key == "invoice:approve").IsActive);
        Assert.Equal(approveId, server.Permissions.Single(permission => permission.Key == "invoice:approve").Id);
    }

    [Theory]
    [InlineData("Facturas", "invoice:read", "invoice:read", "owner")]
    [InlineData("facturas", "invoice-read", "invoice-read", "owner")]
    [InlineData("facturas", "invoice:read", "invoice:delete", "owner")]
    [InlineData("facturas", "invoice:read", "invoice:read", "guest")]
    public void Invalid_manifests_are_rejected(string key, string permission, string rolePermission, string grantTo)
    {
        var manifest = new AccessManifest(
            key,
            "Facturación",
            "facturas-app",
            null,
            [new ManifestPermission(permission, null, null)],
            [new ManifestRole("viewer", "Consulta", null, [rolePermission], [grantTo])]);

        Result<ResourceServer> result = ResourceServer.Register(manifest, Now);

        Assert.Equal("Access.InvalidManifest", result.Error.Code);
    }

    [Fact]
    public void An_assignment_needs_exactly_one_role_and_a_future_expiry()
    {
        Guid tenant = Guid.NewGuid();
        Guid user = Guid.NewGuid();
        Guid admin = Guid.NewGuid();

        Assert.Equal(AccessErrors.InvalidAssignment, RoleAssignment.Grant(tenant, user, null, null, null, admin, Now).Error);
        Assert.Equal(AccessErrors.InvalidAssignment, RoleAssignment.Grant(tenant, user, Guid.NewGuid(), Guid.NewGuid(), null, admin, Now).Error);
        Assert.Equal(AccessErrors.InvalidAssignment, RoleAssignment.Grant(tenant, user, Guid.NewGuid(), null, Now, admin, Now).Error);

        RoleAssignment temporary = RoleAssignment.Grant(tenant, user, Guid.NewGuid(), null, Now.AddHours(1), admin, Now).Value;
        Assert.True(temporary.IsActive(Now));
        Assert.False(temporary.IsActive(Now.AddHours(1)));
    }

    [Fact]
    public void A_role_needs_a_name_and_deduplicates_its_permissions()
    {
        Guid permission = Guid.NewGuid();

        Assert.Equal(AccessErrors.InvalidRoleName, Role.Create(Guid.NewGuid(), "  ", null, [], Now).Error);

        Role role = Role.Create(Guid.NewGuid(), " Contabilidad ", " ", [permission, permission], Now).Value;
        Assert.Equal("Contabilidad", role.Name);
        Assert.Null(role.Description);
        Assert.Equal([permission], role.PermissionIds);
    }

    private static AccessManifest Manifest() => new(
        "facturas",
        "Facturación",
        "facturas-app",
        null,
        [new ManifestPermission("invoice:read", "Ver facturas", null), new ManifestPermission("invoice:approve", "Aprobar", PermissionRequirements.Mfa)],
        [
            new ManifestRole("finance", "Finanzas", null, ["invoice:*"], [BaseRoles.Owner]),
            new ManifestRole("viewer", "Consulta", null, ["invoice:read"], [BaseRoles.Admin, BaseRoles.Member]),
        ]);
}
