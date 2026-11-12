using Herensuge.Modules.Tenancy.Domain.Invitations;
using Herensuge.Modules.Tenancy.Domain.Memberships;
using Herensuge.Modules.Tenancy.Domain.Tenants;

namespace Herensuge.Modules.Tenancy.UnitTests;

public sealed class TenancyTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Acme Corporación S.L.", "acme-corporacion-s-l")]
    [InlineData("  Ñandú & Cía  ", "nandu-cia")]
    [InlineData("!!", "org")]
    public void Slug_is_derived_from_the_name(string name, string expected) =>
        Assert.Equal(expected, TenantSlug.FromName(name).Value);

    [Fact]
    public void The_last_owner_cannot_be_demoted_or_removed()
    {
        Membership owner = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), TenantRole.Owner, Now);

        Assert.Equal(MembershipErrors.LastOwner, owner.ChangeRole(TenantRole.Admin, TenantRole.Owner, ownerCount: 1).Error);
        Assert.Equal(MembershipErrors.LastOwner, owner.Remove(actorRole: null, ownerCount: 1).Error);
        Assert.True(owner.ChangeRole(TenantRole.Admin, TenantRole.Owner, ownerCount: 2).IsSuccess);
    }

    [Fact]
    public void Admins_cannot_touch_owners_or_create_them()
    {
        Membership member = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, Now);
        Membership owner = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), TenantRole.Owner, Now);

        Assert.Equal(MembershipErrors.NotAllowed, member.ChangeRole(TenantRole.Owner, TenantRole.Admin, ownerCount: 1).Error);
        Assert.Equal(MembershipErrors.NotAllowed, owner.Remove(TenantRole.Admin, ownerCount: 2).Error);
        Assert.True(member.ChangeRole(TenantRole.Admin, TenantRole.Admin, ownerCount: 1).IsSuccess);
    }

    [Fact]
    public void Members_manage_nobody()
    {
        Membership other = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, Now);

        Assert.Equal(MembershipErrors.NotAllowed, other.Remove(TenantRole.Member, ownerCount: 1).Error);
    }

    [Fact]
    public void Invitations_are_accepted_once_by_the_invited_verified_email()
    {
        (Invitation invitation, string token) = Invitation.Create(Guid.NewGuid(), "Acme", "ana@example.com", TenantRole.Member, Guid.NewGuid(), Now);

        Assert.Equal(Invitation.HashToken(token), invitation.TokenHash);
        Assert.Equal(InvitationErrors.EmailNotVerified, invitation.Accept(Guid.NewGuid(), "ana@example.com", emailVerified: false, Now).Error);
        Assert.Equal(InvitationErrors.EmailMismatch, invitation.Accept(Guid.NewGuid(), "eve@example.com", emailVerified: true, Now).Error);
        Assert.True(invitation.Accept(Guid.NewGuid(), "ANA@example.com", emailVerified: true, Now).IsSuccess);
        Assert.Equal(InvitationErrors.NotPending, invitation.Accept(Guid.NewGuid(), "ana@example.com", emailVerified: true, Now).Error);
    }

    [Fact]
    public void Invitations_expire_after_seven_days()
    {
        (Invitation invitation, _) = Invitation.Create(Guid.NewGuid(), "Acme", "ana@example.com", TenantRole.Member, Guid.NewGuid(), Now);

        Assert.Equal(
            InvitationErrors.NotPending,
            invitation.Accept(Guid.NewGuid(), "ana@example.com", emailVerified: true, Now + Invitation.Lifetime).Error);
    }

    [Theory]
    [InlineData("owner", TenantRole.Owner)]
    [InlineData(" Admin ", TenantRole.Admin)]
    [InlineData("member", TenantRole.Member)]
    public void Roles_parse_from_claim_values(string value, TenantRole expected)
    {
        Assert.True(TenantRoleRules.TryParse(value, out TenantRole role));
        Assert.Equal(expected, role);
        Assert.Equal(value.Trim().ToLowerInvariant(), role.ToClaimValue());
    }
}
