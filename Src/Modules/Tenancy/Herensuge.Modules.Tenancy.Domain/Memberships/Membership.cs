using Herensuge.Common.Domain;
using Herensuge.Modules.Tenancy.Domain.Memberships.Events;

namespace Herensuge.Modules.Tenancy.Domain.Memberships;

/// <summary>A user belonging to an organization with a role. A user may belong to several organizations.</summary>
public sealed class Membership : AggregateRoot
{
    private Membership()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Membership Create(Guid tenantId, Guid userId, TenantRole role, DateTime utcNow)
    {
        var membership = new Membership
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            CreatedAtUtc = utcNow,
        };

        membership.Raise(new MemberAddedDomainEvent(tenantId, userId, role));
        return membership;
    }

    /// <param name="actorRole">Role of the member making the change.</param>
    /// <param name="ownerCount">Current number of owners of the organization.</param>
    public Result ChangeRole(TenantRole newRole, TenantRole actorRole, int ownerCount)
    {
        if (!actorRole.CanManage(Role) || !actorRole.CanAssign(newRole))
        {
            return MembershipErrors.NotAllowed;
        }

        if (Role == newRole)
        {
            return Result.Success();
        }

        if (Role == TenantRole.Owner && ownerCount <= 1)
        {
            return MembershipErrors.LastOwner;
        }

        TenantRole previous = Role;
        Role = newRole;
        Raise(new MemberRoleChangedDomainEvent(TenantId, UserId, previous, newRole));
        return Result.Success();
    }

    /// <param name="actorRole">Role of whoever removes the member; null when members leave by themselves.</param>
    public Result Remove(TenantRole? actorRole, int ownerCount)
    {
        if (actorRole is { } actor && !actor.CanManage(Role))
        {
            return MembershipErrors.NotAllowed;
        }

        if (Role == TenantRole.Owner && ownerCount <= 1)
        {
            return MembershipErrors.LastOwner;
        }

        Raise(new MemberRemovedDomainEvent(TenantId, UserId));
        return Result.Success();
    }

    /// <summary>The user deleted their account (the last-owner rule was checked before).</summary>
    public void RemoveBecauseUserWasDeleted() => Raise(new MemberRemovedDomainEvent(TenantId, UserId));
}

public enum TenantRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}

public static class TenantRoleRules
{
    // Owners manage everyone; admins manage admins and members; members manage nobody.
    public static bool CanManage(this TenantRole actor, TenantRole target) =>
        actor == TenantRole.Owner || (actor == TenantRole.Admin && target != TenantRole.Owner);

    // Only an owner can make someone else an owner.
    public static bool CanAssign(this TenantRole actor, TenantRole role) =>
        actor == TenantRole.Owner || (actor == TenantRole.Admin && role != TenantRole.Owner);

    public static bool IsAdministrator(this TenantRole role) => role is TenantRole.Admin or TenantRole.Owner;

    /// <summary>Value of the tenant_role claim (owner, admin, member).</summary>
    public static string ToClaimValue(this TenantRole role) => role switch
    {
        TenantRole.Owner => "owner",
        TenantRole.Admin => "admin",
        _ => "member",
    };

    public static bool TryParse(string? value, out TenantRole role)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "owner":
                role = TenantRole.Owner;
                return true;
            case "admin":
                role = TenantRole.Admin;
                return true;
            case "member":
                role = TenantRole.Member;
                return true;
            default:
                role = TenantRole.Member;
                return false;
        }
    }
}

public static class MembershipErrors
{
    public static readonly Error NotFound = Error.NotFound("Memberships.NotFound", "The user is not a member of the organization.");

    public static readonly Error NotAllowed = Error.Forbidden("Memberships.NotAllowed", "Your role does not allow this change.");

    public static readonly Error LastOwner = Error.Failure("Memberships.LastOwner", "An organization must keep at least one owner.");

    public static readonly Error AlreadyMember = Error.Conflict("Memberships.AlreadyMember", "The user is already a member of the organization.");

    public static readonly Error InvalidRole = Error.Validation("Memberships.InvalidRole", "The role must be owner, admin or member.");
}
