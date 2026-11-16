using System.Security.Claims;
using System.Text.Json;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.Presentation.Decisions;

/// <summary>Subject, resource or action of an OpenID AuthZEN request. Properties use snake_case keys.</summary>
public sealed record AuthZenEntity(string Type, string Id, Dictionary<string, JsonElement>? Properties);

public sealed record AuthZenAction(string Name, Dictionary<string, JsonElement>? Properties);

public sealed record EvaluationRequest(AuthZenEntity? Subject, AuthZenAction? Action, AuthZenEntity? Resource);

/// <summary>Batch: the top-level subject is the same for every item; items give their action and resource (or use the top-level ones).</summary>
public sealed record EvaluationsRequest(AuthZenEntity? Subject, AuthZenAction? Action, AuthZenEntity? Resource, IReadOnlyList<EvaluationRequest>? Evaluations);

public sealed record EvaluationResponse(bool Decision, EvaluationContext Context);

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
            Result<EvaluateAccessQuery> query = ToQuery(caller, request.Subject, [request]);
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

            Result<EvaluateAccessQuery> query = ToQuery(caller, request.Subject, items);
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
    }

    internal static string CallerClientId(ClaimsPrincipal caller) =>
        caller.FindFirstValue(HerensugeClaimTypes.ClientId) ?? caller.FindFirstValue(HerensugeClaimTypes.Subject) ?? string.Empty;

    /// <summary>
    /// subject: { type: "user", id: sub, properties: { tenant_id, sid, amr, auth_time } } (the claims of the user's access token).
    /// resource: { type, id, properties: { resource_server? } } and action: { name } → permission "type:name".
    /// </summary>
    private static Result<EvaluateAccessQuery> ToQuery(ClaimsPrincipal caller, AuthZenEntity? subject, IReadOnlyList<EvaluationRequest> items)
    {
        if (subject is not { Type: "user" } || !Guid.TryParse(subject.Id, out Guid userId)
            || !Guid.TryParse(Text(subject.Properties, "tenant_id"), out Guid tenantId))
        {
            return InvalidRequest("subject must be { type: \"user\", id: <sub>, properties: { tenant_id, sid, amr, auth_time } }.");
        }

        if (items.Any(item => string.IsNullOrWhiteSpace(item.Resource?.Type) || string.IsNullOrWhiteSpace(item.Action?.Name)))
        {
            return InvalidRequest("Every evaluation needs resource.type and action.name.");
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
            new AccessSubject(userId, tenantId, sessionId, amr, authTime),
            [.. items.Select(item => new AccessCheck(Text(item.Resource!.Properties, "resource_server"), $"{item.Resource.Type}:{item.Action!.Name}"))]);
    }

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
