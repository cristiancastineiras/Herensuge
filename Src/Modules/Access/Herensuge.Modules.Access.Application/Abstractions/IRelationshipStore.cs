using System.Text.RegularExpressions;

namespace Herensuge.Modules.Access.Application.Abstractions;

/// <summary>Who holds a relation: a user, a service account (client), a group's members or a parent object.</summary>
/// <param name="Type">user, client, group or object.</param>
/// <param name="Id">User id, client_id, group id, or "type/id" of an object of the same application and organization.</param>
public sealed record RelationshipSubject(string Type, string Id)
{
    public const string User = "user";
    public const string Client = "client";
    public const string Group = "group";
    public const string Object = "object";
}

/// <summary>An object of an application in an organization: "invoice" "inv-1".</summary>
public sealed record RelationshipObject(string ResourceServer, Guid TenantId, string Type, string Id);

/// <summary>Result of a relation check. <see cref="FailureReason"/>: relationships_disabled or relationships_unavailable.</summary>
public sealed record RelationCheck(bool Holds, string? FailureReason = null)
{
    public static readonly RelationCheck Missing = new(false);
    public static readonly RelationCheck Held = new(true);
    public static readonly RelationCheck Disabled = new(false, "relationships_disabled");
    public static readonly RelationCheck Unavailable = new(false, "relationships_unavailable");
}

/// <summary>
/// Per-object relationships (plan D10), in OpenFGA when Access:OpenFga:ApiUrl is set. Group memberships are not
/// replicated: callers pass the user's current groups and they travel as contextual tuples. Fails closed.
/// </summary>
public interface IRelationshipStore
{
    bool IsEnabled { get; }

    Task<RelationCheck> CheckAsync(
        RelationshipObject target,
        string relation,
        RelationshipSubject subject,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken);

    /// <summary>Ids of the objects of <paramref name="type"/> the subject has the relation with; null when unavailable.</summary>
    Task<IReadOnlyList<string>?> ListObjectsAsync(
        string resourceServer,
        Guid tenantId,
        string type,
        string relation,
        RelationshipSubject subject,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken);

    /// <summary>False when the store did not answer.</summary>
    Task<bool> WriteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken);

    /// <summary>False when the store did not answer.</summary>
    Task<bool> DeleteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken);

    /// <summary>Every tuple of the subject, in one organization or (null) in all. Throws when the store fails, so the inbox retries.</summary>
    Task DeleteSubjectAsync(Guid? tenantId, RelationshipSubject subject, CancellationToken cancellationToken);
}

/// <summary>
/// Ids in the relationship store, composed by Herensuge so an application can only reach its own organization's
/// objects: object:&lt;app&gt;/&lt;tenantId&gt;/&lt;type&gt;/&lt;id&gt;, user:&lt;id&gt;, client:&lt;client_id&gt;, group:&lt;id&gt;#member.
/// </summary>
public static partial class RelationshipIds
{
    public const string ObjectType = "object";

    public static string Object(RelationshipObject target) =>
        $"{ObjectType}:{target.ResourceServer}/{target.TenantId:D}/{target.Type}/{target.Id}";

    /// <summary>Prefix of every object of an application in an organization.</summary>
    public static string ObjectPrefix(string resourceServer, Guid tenantId) => $"{ObjectType}:{resourceServer}/{tenantId:D}/";

    public static string Subject(RelationshipSubject subject, string resourceServer, Guid tenantId) => subject.Type switch
    {
        RelationshipSubject.User => $"user:{subject.Id}",
        RelationshipSubject.Client => $"client:{subject.Id}",
        RelationshipSubject.Group => $"group:{subject.Id}#member",
        RelationshipSubject.Object => $"{ObjectPrefix(resourceServer, tenantId)}{subject.Id}",
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject.Type, "Unknown subject type."),
    };

    /// <summary>Type and id of an application's object: no separators of the store's syntax, no spaces.</summary>
    public static bool IsValidPart(string? value) => value is { Length: > 0 and <= 128 } && SafePart().IsMatch(value);

    [GeneratedRegex(@"^[A-Za-z0-9_.\-~]+$")]
    private static partial Regex SafePart();
}
