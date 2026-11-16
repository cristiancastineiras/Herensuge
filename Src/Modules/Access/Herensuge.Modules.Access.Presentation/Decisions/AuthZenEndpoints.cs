using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Application.Relationships;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.Presentation.Decisions;

/// <summary>Subject, resource or action of an OpenID AuthZEN request. Properties use snake_case keys.</summary>
public sealed record AuthZenEntity(string Type, string Id, Dictionary<string, JsonElement>? Properties);

public sealed record AuthZenAction(string Name, Dictionary<string, JsonElement>? Properties);

/// <summary>context.ip: the client IP the application saw (plan D9).</summary>
public sealed record AuthZenContext(string? Ip);

public sealed record EvaluationRequest(AuthZenEntity? Subject, AuthZenAction? Action, AuthZenEntity? Resource, AuthZenContext? Context = null);

/// <summary>Batch: the top-level subject is the same for every item; items give their action and resource (or use the top-level ones).</summary>
public sealed record EvaluationsRequest(
    AuthZenEntity? Subject,
    AuthZenAction? Action,
    AuthZenEntity? Resource,
    IReadOnlyList<EvaluationRequest>? Evaluations,
    AuthZenContext? Context = null);

public sealed record EvaluationResponse(bool Decision, EvaluationContext Context);

/// <summary>{ tenantId, object: { type, id }, relation, subject: { type: user|group|client|object, id } } (plan D10).</summary>
public sealed record RelationshipRequest(Guid TenantId, RelationshipEntity? Object, string? Relation, RelationshipEntity? Subject);

public sealed record RelationshipEntity(string Type, string Id);

/// <summary>AuthZEN resource search: resource.type and action.name; no resource.id.</summary>
public sealed record ResourceSearchRequest(AuthZenEntity? Subject, AuthZenAction? Action, AuthZenEntity? Resource, AuthZenContext? Context = null);

public sealed record ResourceSearchItem(string Type, string Id);

/// <param name="AllObjects">A role grants the action on every object; the results are only the objects shared with the subject.</param>
/// <param name="Reason">Why there is nothing, when the subject cannot do the action at all.</param>
public sealed record ResourceSearchContext(bool AllObjects, string? Reason);

public sealed record ResourceSearchResponse(IReadOnlyList<ResourceSearchItem> Results, ResourceSearchContext Context);

public sealed record EvaluationContext(string Reason);

public sealed record EvaluationsResponse(IReadOnlyList<EvaluationResponse> Evaluations);

/// <summary>
/// /access/v1: the decision point (PDP) for the backends of the SaaS applications, with the shape of OpenID AuthZEN
/// (Authorization API). Callers authenticate with client_credentials and the herensuge.access scope, and only ask
/// about their own application.
/// </summary>
// shortcut: one subject per batch call; per-item subjects (AuthZEN allows them) when an application needs them.
internal sealed class AuthZenEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/authzen-configuration", (HttpRequest request) =>
        {
            string origin = $"{request.Scheme}://{request.Host}{request.PathBase}";
            return Results.Ok(new Dictionary<string, string>
            {
                ["policy_decision_point"] = origin,
                ["access_evaluation_endpoint"] = origin + "/access/v1/evaluation",
                ["access_evaluations_endpoint"] = origin + "/access/v1/evaluations",
            });
        }).ExcludeFromDescription();

        RouteGroupBuilder group = app.MapGroup("/access/v1")
            .WithTags("Access (applications)")
            .RequireAuthorization(HerensugePolicies.AccessClient);

        group.MapPost("/evaluation", async (EvaluationRequest request, ClaimsPrincipal caller, IQueryHandler<EvaluateAccessQuery, IReadOnlyList<AccessDecision>> handler, CancellationToken cancellationToken) =>
        {
            Result<EvaluateAccessQuery> query = ToQuery(caller, request.Subject, [request], request.Context);
            if (query.IsFailure)
            {
                return ApiResults.Problem(query);
            }

            return (await handler.Handle(query.Value, cancellationToken)).Match(decisions => Results.Ok(ToResponse(decisions[0])));
        })
            .Produces<EvaluationResponse>();

        group.MapPost("/evaluations", async (EvaluationsRequest request, ClaimsPrincipal caller, IQueryHandler<EvaluateAccessQuery, IReadOnlyList<AccessDecision>> handler, CancellationToken cancellationToken) =>
        {
            IReadOnlyList<EvaluationRequest> items = request.Evaluations is { Count: > 0 } evaluations
                ? [.. evaluations.Select(item => new EvaluationRequest(item.Subject, item.Action ?? request.Action, item.Resource ?? request.Resource))]
                : [new EvaluationRequest(request.Subject, request.Action, request.Resource)];
            if (items.Count > 100 || items.Any(item => item.Subject is not null))
            {
                return ApiResults.Problem(InvalidRequest("At most 100 evaluations per call, all for the top-level subject (no subject per item)."));
            }

            Result<EvaluateAccessQuery> query = ToQuery(caller, request.Subject, items, request.Context);
            if (query.IsFailure)
            {
                return ApiResults.Problem(query);
            }

            return (await handler.Handle(query.Value, cancellationToken))
                .Match(decisions => Results.Ok(new EvaluationsResponse([.. decisions.Select(ToResponse)])));
        })
            .Produces<EvaluationsResponse>();

        // The application's CI syncs its own manifest (herensuge.access.json).
        group.MapPut("/manifest", async (AccessManifest manifest, ClaimsPrincipal caller, ICommandHandler<SyncManifestCommand, ManifestSyncResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SyncManifestCommand(manifest, CallerClientId(caller)), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<ManifestSyncResponse>();

        // Plan D10: the application shares its objects. The subject must belong to the organization.
        group.MapPut("/relationships", (RelationshipRequest request, ClaimsPrincipal caller, ICommandHandler<WriteRelationshipCommand> handler, CancellationToken cancellationToken) =>
            WriteRelationshipAsync(request, caller, handler, delete: false, cancellationToken))
            .Produces(StatusCodes.Status204NoContent);

        // DELETE with a body (as in AuthZEN): explicit, because minimal APIs do not infer it for DELETE.
        group.MapDelete("/relationships", ([Microsoft.AspNetCore.Mvc.FromBody] RelationshipRequest request, ClaimsPrincipal caller, ICommandHandler<WriteRelationshipCommand> handler, CancellationToken cancellationToken) =>
            WriteRelationshipAsync(request, caller, handler, delete: true, cancellationToken))
            .Produces(StatusCodes.Status204NoContent);

        // AuthZEN resource search: the objects of a type the subject can do the action on.
        group.MapPost("/search/resource", async (ResourceSearchRequest request, ClaimsPrincipal caller, IQueryHandler<SearchResourcesQuery, ResourceSearchResult> handler, CancellationToken cancellationToken) =>
        {
            Result<EvaluateAccessQuery> subject = ToQuery(caller, request.Subject, [new EvaluationRequest(request.Subject, request.Action, request.Resource)], request.Context);
            if (subject.IsFailure)
            {
                return ApiResults.Problem(subject);
            }

            Result<ResourceSearchResult> found = await handler.Handle(
                new SearchResourcesQuery(CallerClientId(caller), subject.Value.Subject, request.Resource!.Type, request.Action!.Name),
                cancellationToken);
            return found.Match(result => Results.Ok(new ResourceSearchResponse(
                [.. result.Ids.Select(id => new ResourceSearchItem(request.Resource.Type, id))],
                new ResourceSearchContext(result.AllObjects, result.Reason))));
        })
            .Produces<ResourceSearchResponse>();
    }

    private static async Task<IResult> WriteRelationshipAsync(
        RelationshipRequest request,
        ClaimsPrincipal caller,
        ICommandHandler<WriteRelationshipCommand> handler,
        bool delete,
        CancellationToken cancellationToken) =>
        request is { Object: { } target, Subject: { } subject, Relation: { } relation }
            ? (await handler.Handle(
                    new WriteRelationshipCommand(CallerClientId(caller), request.TenantId, target.Type, target.Id, relation, subject.Type, subject.Id, delete),
                    cancellationToken))
                .Match(Results.NoContent)
            : ApiResults.Problem(AccessErrors.InvalidRelationship("tenantId, object { type, id }, relation and subject { type, id } are required."));

    internal static string CallerClientId(ClaimsPrincipal caller) =>
        caller.FindFirstValue(HerensugeClaimTypes.ClientId) ?? caller.FindFirstValue(HerensugeClaimTypes.Subject) ?? string.Empty;

    /// <summary>
    /// subject: { type: "user", id: sub, properties: { tenant_id, sid, amr, auth_time, act? } } (the claims of the user's
    /// access token) or { type: "client", id: client_id, properties: { tenant_id } } (a service account, plan D7).
    /// resource: { type, id, properties: { resource_server? } } and action: { name } → permission "type:name".
    /// context: { ip } → the client IP for trusted_network (plan D9).
    /// </summary>
    private static Result<EvaluateAccessQuery> ToQuery(ClaimsPrincipal caller, AuthZenEntity? subject, IReadOnlyList<EvaluationRequest> items, AuthZenContext? context)
    {
        if (subject is null || !Guid.TryParse(Text(subject.Properties, "tenant_id"), out Guid tenantId)
            || !(subject.Type is "user" && Guid.TryParse(subject.Id, out _) || subject.Type is "client" && !string.IsNullOrWhiteSpace(subject.Id)))
        {
            return InvalidRequest(
                "subject must be { type: \"user\", id: <sub>, properties: { tenant_id, sid, amr, auth_time } } or { type: \"client\", id: <client_id>, properties: { tenant_id } }.");
        }

        if (items.Any(item => string.IsNullOrWhiteSpace(item.Resource?.Type) || string.IsNullOrWhiteSpace(item.Action?.Name)))
        {
            return InvalidRequest("Every evaluation needs resource.type and action.name.");
        }

        AccessCheck[] checks =
        [
            .. items.Select(item => new AccessCheck(
                Text(item.Resource!.Properties, "resource_server"),
                $"{item.Resource.Type}:{item.Action!.Name}",
                string.IsNullOrEmpty(item.Resource.Id) ? null : item.Resource.Id)),
        ];
        IPAddress? ip = IPAddress.TryParse(context?.Ip, out IPAddress? parsed) ? parsed : null;
        if (subject.Type == "client")
        {
            return new EvaluateAccessQuery(CallerClientId(caller), new PdpSubject(tenantId, null, subject.Id, null, [], null, null, ip), checks);
        }

        Guid? sessionId = Guid.TryParse(Text(subject.Properties, "sid"), out Guid sid) ? sid : null;
        string[] amr = subject.Properties?.TryGetValue("amr", out JsonElement methods) == true && methods.ValueKind == JsonValueKind.Array
            ? [.. methods.EnumerateArray().Where(method => method.ValueKind == JsonValueKind.String).Select(method => method.GetString()!)]
            : [];
        DateTimeOffset? authTime = subject.Properties?.TryGetValue("auth_time", out JsonElement time) == true && time.TryGetInt64(out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

        return new EvaluateAccessQuery(
            CallerClientId(caller),
            new PdpSubject(tenantId, Guid.Parse(subject.Id), null, sessionId, amr, authTime, ActorOf(subject.Properties), ip),
            checks);
    }

    /// <summary>"act": { "sub": … } of the user's token (support session or token exchange), when the PEP passes it.</summary>
    private static string? ActorOf(Dictionary<string, JsonElement>? properties) =>
        properties?.TryGetValue("act", out JsonElement act) == true && act.ValueKind == JsonValueKind.Object
            && act.TryGetProperty("sub", out JsonElement sub) && sub.ValueKind == JsonValueKind.String
            ? sub.GetString()
            : null;

    private static string? Text(Dictionary<string, JsonElement>? properties, string name) =>
        properties?.TryGetValue(name, out JsonElement value) == true
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private static EvaluationResponse ToResponse(AccessDecision decision) => new(decision.Decision, new EvaluationContext(decision.Reason));

    private static Error InvalidRequest(string description) => Error.Failure("Access.InvalidEvaluationRequest", description);
}
