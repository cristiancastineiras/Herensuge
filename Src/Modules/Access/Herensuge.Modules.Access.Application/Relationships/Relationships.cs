using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Contracts.IntegrationEvents;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Identity.Contracts.IntegrationEvents;
using Herensuge.Modules.Tenancy.Contracts;
using Herensuge.Modules.Tenancy.Contracts.IntegrationEvents;

namespace Herensuge.Modules.Access.Application.Relationships;

/// <summary>
/// An application's backend writes (or deletes) a relation on one of its objects (plan D10). The application is the
/// caller's; the subject must belong to the organization: a member, one of its groups, one of its service accounts or
/// (relation "parent") another object of the same application and organization.
/// </summary>
public sealed record WriteRelationshipCommand(
    string CallerClientId,
    Guid TenantId,
    string ObjectType,
    string ObjectId,
    string Relation,
    string SubjectType,
    string SubjectId,
    bool Delete) : ICommand;

/// <summary>AuthZEN resource search: the objects of a type the subject can do the action on.</summary>
public sealed record SearchResourcesQuery(string CallerClientId, PdpSubject Subject, string Type, string Action) : IQuery<ResourceSearchResult>;

/// <param name="AllObjects">A role grants the action on every object (relationMode "or"): <see cref="Ids"/> lists only the shared ones.</param>
/// <param name="Reason">Why nothing was found when the subject cannot do the action at all (not_member, step_up_required…).</param>
public sealed record ResourceSearchResult(IReadOnlyList<string> Ids, bool AllObjects, string? Reason);

internal sealed class WriteRelationshipCommandHandler(
    IResourceServerRepository servers,
    IGroupRepository groups,
    IServiceAccountRepository serviceAccounts,
    ITenancyModuleApi tenancy,
    IRelationshipStore relationships,
    IEventBus eventBus) : ICommandHandler<WriteRelationshipCommand>
{
    public async Task<Result> Handle(WriteRelationshipCommand command, CancellationToken cancellationToken)
    {
        if (await servers.GetByClientIdAsync(command.CallerClientId, cancellationToken) is not { } server)
        {
            return AccessErrors.NotYourResourceServer;
        }

        if (!relationships.IsEnabled)
        {
            return AccessErrors.RelationshipsDisabled;
        }

        Result valid = await ValidateAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return valid;
        }

        var target = new RelationshipObject(server.Key, command.TenantId, command.ObjectType, command.ObjectId);
        var subject = new RelationshipSubject(command.SubjectType, command.SubjectId);
        bool stored = command.Delete
            ? await relationships.DeleteAsync(target, command.Relation, subject, cancellationToken)
            : await relationships.WriteAsync(target, command.Relation, subject, cancellationToken);
        if (!stored)
        {
            return AccessErrors.RelationshipsUnavailable;
        }

        IntegrationEvent written = command.Delete
            ? new RelationshipDeletedIntegrationEvent(command.TenantId, server.Key, command.ObjectType, command.ObjectId, command.Relation, command.SubjectType, command.SubjectId)
            : new RelationshipWrittenIntegrationEvent(command.TenantId, server.Key, command.ObjectType, command.ObjectId, command.Relation, command.SubjectType, command.SubjectId);
        await eventBus.PublishAsync(written, cancellationToken);
        return Result.Success();
    }

    private async Task<Result> ValidateAsync(WriteRelationshipCommand command, CancellationToken cancellationToken)
    {
        if (!RelationshipIds.IsValidPart(command.ObjectType) || !RelationshipIds.IsValidPart(command.ObjectId))
        {
            return AccessErrors.InvalidRelationship("object.type and object.id use letters, digits and . _ - ~ (max 128).");
        }

        bool parent = command.Relation == Relations.Parent;
        if (command.Relation is not (Relations.Owner or Relations.Editor or Relations.Viewer or Relations.Parent)
            || parent != (command.SubjectType == RelationshipSubject.Object))
        {
            return AccessErrors.InvalidRelationship("relation is owner, editor or viewer for a user, group or client, and parent for an object.");
        }

        bool belongs = command.SubjectType switch
        {
            RelationshipSubject.User => Guid.TryParse(command.SubjectId, out Guid userId)
                && await tenancy.GetMembershipAsync(command.TenantId, userId, cancellationToken) is not null,
            RelationshipSubject.Group => Guid.TryParse(command.SubjectId, out Guid groupId)
                && await groups.GetAsync(command.TenantId, groupId, cancellationToken) is not null,
            RelationshipSubject.Client => (await serviceAccounts.GetByClientIdAsync(command.SubjectId, cancellationToken))?.TenantId == command.TenantId,
            RelationshipSubject.Object => command.SubjectId.Split('/') is [{ } type, { } id] && RelationshipIds.IsValidPart(type) && RelationshipIds.IsValidPart(id),
            _ => false,
        };
        return belongs
            ? Result.Success()
            : AccessErrors.InvalidRelationship("subject must be a member, group or service account of the organization (or \"type/id\" of an object).");
    }
}

internal sealed class SearchResourcesQueryHandler(
    IResourceServerRepository servers,
    IGroupRepository groups,
    IRelationshipStore relationships,
    IQueryHandler<EvaluateAccessQuery, IReadOnlyList<AccessDecision>> decisions) : IQueryHandler<SearchResourcesQuery, ResourceSearchResult>
{
    public async Task<Result<ResourceSearchResult>> Handle(SearchResourcesQuery query, CancellationToken cancellationToken)
    {
        if (await servers.GetByClientIdAsync(query.CallerClientId, cancellationToken) is not { } server)
        {
            return AccessErrors.NotYourResourceServer;
        }

        string key = $"{query.Type}:{query.Action}";
        if (server.ActivePermissions.FirstOrDefault(permission => permission.Key == key)?.Relation is not { } relation)
        {
            return AccessErrors.InvalidRelationship($"«{key}» is not granted per object (it declares no relation).");
        }

        // Every other step (session, organization, plan, role) through the decision point itself, without an object.
        Result<IReadOnlyList<AccessDecision>> probe = await decisions.Handle(
            new EvaluateAccessQuery(query.CallerClientId, query.Subject, [new AccessCheck(null, key)]),
            cancellationToken);
        if (probe.IsFailure)
        {
            return probe.Error;
        }

        AccessDecision decision = probe.Value[0];
        if (decision.Decision)
        {
            // Granted without an object: the role suffices for every object (relationMode "or").
            return new ResourceSearchResult([], AllObjects: true, null);
        }

        // Only a missing relation can be made up for by the objects shared with the subject.
        if (decision.Reason != AccessReasons.NoRelation)
        {
            return new ResourceSearchResult([], AllObjects: false, decision.Reason);
        }

        PdpSubject subject = query.Subject;
        IReadOnlyList<string>? ids = subject.ClientId is { } clientId
            ? await relationships.ListObjectsAsync(server.Key, subject.TenantId, query.Type, relation, new RelationshipSubject(RelationshipSubject.Client, clientId), [], cancellationToken)
            : await relationships.ListObjectsAsync(
                server.Key,
                subject.TenantId,
                query.Type,
                relation,
                new RelationshipSubject(RelationshipSubject.User, subject.UserId!.Value.ToString()),
                [.. (await groups.ListByMemberAsync(subject.TenantId, subject.UserId.Value, cancellationToken)).Select(group => group.Id)],
                cancellationToken);
        return ids is null
            ? new ResourceSearchResult([], AllObjects: false, RelationCheck.Unavailable.FailureReason)
            : new ResourceSearchResult(ids, AllObjects: false, null);
    }
}

/// <summary>
/// Removes the tuples of subjects that leave (plan D10). Best effort with retries (outbox and inbox): the decision
/// already denies them at step 3, so a late cleanup never grants anything.
/// </summary>
internal sealed class RemoveRelationshipsHandlers(IRelationshipStore relationships) :
    IIntegrationEventHandler<MemberRemovedIntegrationEvent>,
    IIntegrationEventHandler<UserDeletedIntegrationEvent>,
    IDomainEventHandler<GroupDeletedDomainEvent>,
    IDomainEventHandler<ServiceAccountDeletedDomainEvent>
{
    public Task Handle(MemberRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        DeleteAsync(integrationEvent.TenantId, RelationshipSubject.User, integrationEvent.UserId.ToString(), cancellationToken);

    public Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        DeleteAsync(null, RelationshipSubject.User, integrationEvent.UserId.ToString(), cancellationToken);

    public Task Handle(GroupDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        DeleteAsync(domainEvent.TenantId, RelationshipSubject.Group, domainEvent.GroupId.ToString(), cancellationToken);

    public Task Handle(ServiceAccountDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        DeleteAsync(domainEvent.TenantId, RelationshipSubject.Client, domainEvent.ClientId, cancellationToken);

    private Task DeleteAsync(Guid? tenantId, string type, string id, CancellationToken cancellationToken) =>
        relationships.IsEnabled
            ? relationships.DeleteSubjectAsync(tenantId, new RelationshipSubject(type, id), cancellationToken)
            : Task.CompletedTask;
}
