using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Domain.Roles.Events;

namespace Herensuge.Modules.Access.Domain.Roles;

/// <summary>
/// A member of an organization holds a role: one of the organization (<see cref="RoleId"/>) or one offered by an
/// application (<see cref="RoleTemplateId"/>). Optionally until a date (temporary access).
/// </summary>
public sealed class RoleAssignment : AggregateRoot
{
    private RoleAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? RoleId { get; private set; }

    public Guid? RoleTemplateId { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public Guid GrantedByUserId { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }

    public static Result<RoleAssignment> Grant(
        Guid tenantId,
        Guid userId,
        Guid? roleId,
        Guid? roleTemplateId,
        DateTime? expiresAtUtc,
        Guid grantedByUserId,
        DateTime utcNow)
    {
        if ((roleId is null) == (roleTemplateId is null) || expiresAtUtc <= utcNow)
        {
            return AccessErrors.InvalidAssignment;
        }

        var assignment = new RoleAssignment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            RoleTemplateId = roleTemplateId,
            ExpiresAtUtc = expiresAtUtc,
            GrantedByUserId = grantedByUserId,
            GrantedAtUtc = utcNow,
        };

        assignment.Raise(new RoleAssignedDomainEvent(tenantId, userId, roleId, roleTemplateId, expiresAtUtc));
        return assignment;
    }

    public bool IsActive(DateTime utcNow) => ExpiresAtUtc is null || ExpiresAtUtc > utcNow;

    public bool IsSameGrant(Guid? roleId, Guid? roleTemplateId, DateTime? expiresAtUtc) =>
        RoleId == roleId && RoleTemplateId == roleTemplateId && ExpiresAtUtc == expiresAtUtc;

    public void Revoke() => Raise(new RoleUnassignedDomainEvent(TenantId, UserId, RoleId, RoleTemplateId));
}
