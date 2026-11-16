using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Herensuge.Access.AspNetCore;

/// <summary>Answer of the decision point. <see cref="Reason"/>: granted, no_role, step_up_required, condition_not_met…</summary>
public sealed record AccessCheckResult(bool Allowed, string Reason)
{
    public const string StepUpRequired = "step_up_required";

    public const string Unavailable = "pdp_unavailable";
}

/// <summary>Herensuge's decision point (PDP) for this application.</summary>
public interface IHerensugeAccess
{
    /// <summary>
    /// Can <paramref name="user"/> (the principal of a token of this application) do <paramref name="permission"/>
    /// ("resource:action"), optionally on <paramref name="resourceId"/>? <paramref name="ipAddress"/> is the client's,
    /// for the organization's trusted networks.
    /// </summary>
    Task<AccessCheckResult> CheckAsync(
        ClaimsPrincipal user,
        string permission,
        string? resourceId = null,
        IPAddress? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shares an object of this application: <paramref name="relation"/> (owner, editor, viewer; parent for an object)
    /// for a subject of the organization (user id, group id, a service account's client_id, or "type/id" of an object).
    /// </summary>
    Task WriteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default);

    Task DeleteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default);
}

/// <param name="SubjectType">user, group, client or object.</param>
public sealed record Relationship(string ObjectType, string ObjectId, string Relation, string SubjectType, string SubjectId);

/// <summary>
/// Calls /access/v1/evaluation with the application's own token (client_credentials, scope herensuge.access), cached
/// until a minute before it expires. Fails closed: an error or a timeout is a deny.
/// </summary>
internal sealed class HerensugeAccessClient(
    IHttpClientFactory httpClientFactory,
    IOptions<HerensugeAccessOptions> options,
    TimeProvider timeProvider,
    ILogger<HerensugeAccessClient> logger) : IHerensugeAccess, IDisposable
{
    internal const string HttpClientName = "Herensuge.Access";

    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private (string Value, DateTimeOffset ExpiresAt)? _token;

    public async Task<AccessCheckResult> CheckAsync(
        ClaimsPrincipal user,
        string permission,
        string? resourceId = null,
        IPAddress? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        int separator = permission.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new ArgumentException("A permission is \"resource:action\".", nameof(permission));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "access/v1/evaluation")
            {
                Content = JsonContent.Create(Evaluation(user, permission[..separator], permission[(separator + 1)..], resourceId, ipAddress)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(timeout.Token));

            using HttpResponseMessage response = await Client().SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _token = null;
            }

            response.EnsureSuccessStatusCode();
            EvaluationResponse? decision = await response.Content.ReadFromJsonAsync<EvaluationResponse>(JsonSerializerOptions.Web, timeout.Token);
            return new AccessCheckResult(decision?.Decision == true, decision?.Context?.Reason ?? AccessCheckResult.Unavailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Herensuge decision point unavailable for {Permission}; denying", permission);
            return new AccessCheckResult(false, AccessCheckResult.Unavailable);
        }
    }

    public Task WriteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default) =>
        SendRelationshipAsync(HttpMethod.Put, tenantId, relationship, cancellationToken);

    public Task DeleteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default) =>
        SendRelationshipAsync(HttpMethod.Delete, tenantId, relationship, cancellationToken);

    public void Dispose() => _tokenLock.Dispose();

    /// <summary>Writes are not decisions: errors throw (HttpRequestException) instead of failing closed.</summary>
    private async Task SendRelationshipAsync(HttpMethod method, Guid tenantId, Relationship relationship, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "access/v1/relationships")
        {
            Content = JsonContent.Create(new
            {
                tenantId,
                @object = new { type = relationship.ObjectType, id = relationship.ObjectId },
                relation = relationship.Relation,
                subject = new { type = relationship.SubjectType, id = relationship.SubjectId },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));
        using HttpResponseMessage response = await Client().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// AuthZEN request with the subject properties the PDP reads from the token: a user, or a service account (a
    /// machine token: sub = client_id, no session).
    /// </summary>
    internal static JsonObject Evaluation(ClaimsPrincipal user, string type, string action, string? resourceId, IPAddress? ipAddress)
    {
        string? subject = user.FindFirstValue(HerensugeClaims.Subject);
        bool machine = subject is not null && subject == user.FindFirstValue(HerensugeClaims.ClientId);
        var properties = machine ? new JsonObject { ["tenant_id"] = user.FindFirstValue(HerensugeClaims.TenantId) } : new JsonObject
        {
            ["tenant_id"] = user.FindFirstValue(HerensugeClaims.TenantId),
            ["sid"] = user.FindFirstValue(HerensugeClaims.SessionId),
            ["amr"] = new JsonArray([.. HerensugeClaims.Values(user, HerensugeClaims.AuthenticationMethods).Select(method => JsonValue.Create(method))]),
        };
        if (!machine && long.TryParse(user.FindFirstValue(HerensugeClaims.AuthenticationTime), out long authTime))
        {
            properties["auth_time"] = authTime;
        }

        if (!machine && user.FindFirstValue(HerensugeClaims.Actor) is { Length: > 0 } act && act.StartsWith('{'))
        {
            properties["act"] = JsonNode.Parse(act);
        }

        var evaluation = new JsonObject
        {
            ["subject"] = new JsonObject { ["type"] = machine ? "client" : "user", ["id"] = subject, ["properties"] = properties },
            ["action"] = new JsonObject { ["name"] = action },
            ["resource"] = new JsonObject { ["type"] = type, ["id"] = resourceId ?? string.Empty },
        };
        if (ipAddress is not null)
        {
            evaluation["context"] = new JsonObject { ["ip"] = ipAddress.ToString() };
        }

        return evaluation;
    }

    private HttpClient Client() => httpClientFactory.CreateClient(HttpClientName);

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached && cached.ExpiresAt - ExpiryMargin > timeProvider.GetUtcNow())
        {
            return cached.Value;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is { } refreshed && refreshed.ExpiresAt - ExpiryMargin > timeProvider.GetUtcNow())
            {
                return refreshed.Value;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "herensuge.access",
                ["client_id"] = options.Value.ClientId,
                ["client_secret"] = options.Value.ClientSecret,
            });
            using HttpResponseMessage response = await Client().PostAsync("connect/token", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            TokenResponse token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new JsonException("Empty token response.");

            _token = (token.AccessToken, timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn));
            return token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record EvaluationResponse(bool Decision, DecisionContext? Context);

    private sealed record DecisionContext(string? Reason);
}
