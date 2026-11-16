using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Domain.Roles.Events;

namespace Herensuge.Modules.Access.Domain.Roles;

/// <summary>Who can hold a role (plan D6, D7).</summary>
public static class SubjectTypes
{
    /// <summary>A member of the organization.</summary>
    public const string User = "user";

    /// <summary>A group of the organization: its members hold the role.</summary>
    public const string Group = "group";

    /// <summary>A service account of the organization (client_credentials).</summary>
    public const string Client = "client";
}

/// <summary>
/// A subject of an organization (a member, a group or a service account) holds a role: one of the organization
/// (<see cref="RoleId"/>) or one offered by an application (<see cref="RoleTemplateId"/>). Optionally until a date.
/// </summary>
public sealed class RoleAssignment : AggregateRoot
{
    private RoleAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>One of <see cref="SubjectTypes"/>.</summary>
    public string SubjectType { get; private set; } = SubjectTypes.User;

    /// <summary>User id, group id or service account id.</summary>
    public Guid SubjectId { get; private set; }

    public Guid? RoleId { get; private set; }

    public Guid? RoleTemplateId { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public Guid GrantedByUserId { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }

    public static Result<RoleAssignment> Grant(
        Guid tenantId,
        string subjectType,
        Guid subjectId,
        Guid? roleId,
        Guid? roleTemplateId,
        DateTime? expiresAtUtc,
        Guid grantedByUserId,
        DateTime utcNow)
    {
        if ((roleId is null) == (roleTemplateId is null) || expiresAtUtc <= utcNow
            || subjectType is not (SubjectTypes.User or SubjectTypes.Group or SubjectTypes.Client))
        {
            return AccessErrors.InvalidAssignment;
        }

        var assignment = new RoleAssignment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            RoleId = roleId,
            RoleTemplateId = roleTemplateId,
            ExpiresAtUtc = expiresAtUtc,
            GrantedByUserId = grantedByUserId,
            GrantedAtUtc = utcNow,
        };

        assignment.Raise(new RoleAssignedDomainEvent(tenantId, subjectType, subjectId, roleId, roleTemplateId, expiresAtUtc));
        return assignment;
    }

    public bool IsActive(DateTime utcNow) => ExpiresAtUtc is null || ExpiresAtUtc > utcNow;

    public bool IsSameGrant(Guid? roleId, Guid? roleTemplateId, DateTime? expiresAtUtc) =>
        RoleId == roleId && RoleTemplateId == roleTemplateId && ExpiresAtUtc == expiresAtUtc;

    public void Revoke() => Raise(new RoleUnassignedDomainEvent(TenantId, SubjectType, SubjectId, RoleId, RoleTemplateId));
}
