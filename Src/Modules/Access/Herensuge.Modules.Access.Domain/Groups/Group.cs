using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.Groups;

/// <summary>Members of an organization that hold roles together (plan D6).</summary>
public sealed class Group : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxMembers = 1000;
    public const int MaxPerTenant = 100;

    private Group()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid[] MemberUserIds { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Group> Create(Guid tenantId, string name, string? description, DateTime utcNow)
    {
        var group = new Group { Id = Guid.CreateVersion7(), TenantId = tenantId, CreatedAtUtc = utcNow };
        Result set = group.Set(name, description);
        if (set.IsFailure)
        {
            return set.Error;
        }

        group.Raise(new GroupCreatedDomainEvent(tenantId, group.Id, group.Name));
        return group;
    }

    public Result Update(string name, string? description)
    {
        Result set = Set(name, description);
        if (set.IsSuccess)
        {
            Raise(new GroupUpdatedDomainEvent(TenantId, Id, Name));
        }

        return set;
    }

    /// <summary>Replaces the members. The caller has checked they belong to the organization.</summary>
    public Result SetMembers(IReadOnlyCollection<Guid> userIds)
    {
        Guid[] members = [.. userIds.Distinct()];
        if (members.Length > MaxMembers)
        {
            return AccessErrors.TooManyGroupMembers;
        }

        Guid[] added = [.. members.Except(MemberUserIds)];
        Guid[] removed = [.. MemberUserIds.Except(members)];
        if (added.Length == 0 && removed.Length == 0)
        {
            return Result.Success();
        }

        MemberUserIds = members;
        Raise(new GroupMembersChangedDomainEvent(TenantId, Id, added, removed));
        return Result.Success();
    }

    /// <summary>A member who leaves the organization leaves its groups (plan D6).</summary>
    public void RemoveMember(Guid userId)
    {
        if (MemberUserIds.Contains(userId))
        {
            MemberUserIds = [.. MemberUserIds.Where(id => id != userId)];
            Raise(new GroupMembersChangedDomainEvent(TenantId, Id, [], [userId]));
        }
    }

    /// <summary>The caller removes the group's assignments in the same unit of work.</summary>
    public void Delete() => Raise(new GroupDeletedDomainEvent(TenantId, Id, Name));

    private Result Set(string name, string? description)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            return AccessErrors.InvalidGroupName;
        }

        Name = trimmed;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return Result.Success();
    }
}

public sealed record GroupCreatedDomainEvent(Guid TenantId, Guid GroupId, string Name) : DomainEvent;

public sealed record GroupUpdatedDomainEvent(Guid TenantId, Guid GroupId, string Name) : DomainEvent;

public sealed record GroupDeletedDomainEvent(Guid TenantId, Guid GroupId, string Name) : DomainEvent;

public sealed record GroupMembersChangedDomainEvent(Guid TenantId, Guid GroupId, IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed) : DomainEvent;
