using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Herensuge.Access.AspNetCore.UnitTests;

public sealed class AccessSdkTests
{
    private static readonly HerensugeAccessOptions Options = new()
    {
        Authority = "https://id.test/",
        ResourceServer = "facturas",
        ClientId = "facturas-app",
        ClientSecret = "secret",
    };

    private static ClaimsPrincipal User(params Claim[] extra) => new(new ClaimsIdentity(
        [
            new Claim("sub", "0198a1b2-0000-7000-8000-000000000001"),
            new Claim("tenant_id", "0198a1b2-0000-7000-8000-000000000002"),
            new Claim("sid", "0198a1b2-0000-7000-8000-000000000003"),
            new Claim("amr", "pwd"),
            new Claim("auth_time", "1763287200"),
            .. extra,
        ],
        authenticationType: "Bearer"));

    [Fact]
    public async Task A_permission_in_the_token_is_allowed_without_asking()
    {
        var pdp = new FakeAccess(new AccessCheckResult(false, "no_role"));
        AuthorizationHandlerContext context = await AuthorizeAsync(pdp, User(new Claim("permissions", "invoice:read")), "invoice:read");

        Assert.True(context.HasSucceeded);
        Assert.Equal(0, pdp.Calls);
    }

    [Fact]
    public async Task Anything_else_is_decided_by_the_decision_point()
    {
        var pdp = new FakeAccess(new AccessCheckResult(false, AccessCheckResult.StepUpRequired));
        AuthorizationHandlerContext context = await AuthorizeAsync(
            pdp,
            User(new Claim("permissions", "invoice:read"), new Claim("conditional_permissions", "invoice:approve")),
            "invoice:approve");

        Assert.False(context.HasSucceeded);
        Assert.Equal(1, pdp.Calls);
        Assert.Equal(AccessCheckResult.StepUpRequired, Assert.Single(context.FailureReasons).Message);
    }

    [Fact]
    public async Task The_machine_token_is_cached_and_the_client_ip_is_sent()
    {
        var backend = new FakeHerensuge();
        HerensugeAccessClient client = Client(backend);

        AccessCheckResult first = await client.CheckAsync(User(), "invoice:approve", "inv-1", IPAddress.Parse("203.0.113.7"), TestContext.Current.CancellationToken);
        AccessCheckResult second = await client.CheckAsync(User(), "invoice:approve", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
        Assert.Equal(1, backend.TokenRequests);
        Assert.Equal(2, backend.Evaluations.Count);

        JsonElement evaluation = backend.Evaluations[0];
        Assert.Equal("invoice", evaluation.GetProperty("resource").GetProperty("type").GetString());
        Assert.Equal("inv-1", evaluation.GetProperty("resource").GetProperty("id").GetString());
        Assert.Equal("approve", evaluation.GetProperty("action").GetProperty("name").GetString());
        Assert.Equal("203.0.113.7", evaluation.GetProperty("context").GetProperty("ip").GetString());
        Assert.Equal(1763287200, evaluation.GetProperty("subject").GetProperty("properties").GetProperty("auth_time").GetInt64());
    }

    [Fact]
    public async Task An_unavailable_decision_point_denies()
    {
        HerensugeAccessClient client = Client(new FakeHerensuge { Down = true });

        AccessCheckResult result = await client.CheckAsync(User(), "invoice:approve", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Allowed);
        Assert.Equal(AccessCheckResult.Unavailable, result.Reason);
    }

    [Fact]
    public async Task The_user_name_comes_from_userinfo_with_the_users_token()
    {
        var backend = new FakeHerensuge();

        HerensugeUserInfo info = await Client(backend).GetUserInfoAsync("user-token", TestContext.Current.CancellationToken);

        Assert.Equal("Ana Pérez", info.Name);
        Assert.Equal("ana@example.com", info.Email);
        Assert.Equal("Bearer user-token", backend.UserInfoAuthorization);
    }

    [Fact]
    public async Task The_backchannel_sends_calls_to_the_authority_through_the_internal_address()
    {
        var seen = new List<HttpRequestMessage>();
        using var handler = new BackchannelRewriteHandler(new Uri("https://id.example.com/"), new Uri("http://herensuge-api:8080"))
        {
            InnerHandler = new Recorder(seen),
        };
        using var client = new HttpClient(handler);

        using HttpResponseMessage toHerensuge = await client.GetAsync(new Uri("https://id.example.com/.well-known/jwks"), TestContext.Current.CancellationToken);
        using HttpResponseMessage elsewhere = await client.GetAsync(new Uri("https://api.example.com/x"), TestContext.Current.CancellationToken);

        Assert.Equal("http://herensuge-api:8080/.well-known/jwks", seen[0].RequestUri!.ToString());
        Assert.Equal("https", Assert.Single(seen[0].Headers.GetValues("X-Forwarded-Proto")));
        Assert.Equal("id.example.com", Assert.Single(seen[0].Headers.GetValues("X-Forwarded-Host")));
        Assert.Equal("https://api.example.com/x", seen[1].RequestUri!.ToString());
        Assert.False(seen[1].Headers.Contains("X-Forwarded-Host"));
    }

    private sealed class Recorder(List<HttpRequestMessage> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(IHerensugeAccess access, ClaimsPrincipal user, string permission)
    {
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, new DefaultHttpContext());
        await new PermissionHandler(access).HandleAsync(context);
        return context;
    }

    private static HerensugeAccessClient Client(FakeHerensuge backend) =>
        new(backend, Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System, NullLogger<HerensugeAccessClient>.Instance);

    private sealed class FakeAccess(AccessCheckResult answer) : IHerensugeAccess
    {
        public int Calls { get; private set; }

        public Task<AccessCheckResult> CheckAsync(ClaimsPrincipal user, string permission, string? resourceId = null, IPAddress? ipAddress = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(answer);
        }

        public Task WriteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteRelationshipAsync(Guid tenantId, Relationship relationship, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> ExchangeTokenAsync(string subjectToken, string targetApplication, CancellationToken cancellationToken = default) =>
            Task.FromResult(subjectToken);

        public Task<HerensugeUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HerensugeUserInfo("user", "Ana", null));
    }

    /// <summary>Herensuge's token endpoint and decision point, as an HttpClient factory.</summary>
    private sealed class FakeHerensuge : HttpMessageHandler, IHttpClientFactory
    {
        public bool Down { get; init; }

        public int TokenRequests { get; private set; }

        public List<JsonElement> Evaluations { get; } = [];

        public string? UserInfoAuthorization { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri(Options.Authority) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Down)
            {
                throw new HttpRequestException("Connection refused.");
            }

            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                TokenRequests++;
                return Json("""{ "access_token": "machine-token", "token_type": "Bearer", "expires_in": 3600 }""");
            }

            if (request.RequestUri.AbsolutePath == "/connect/userinfo")
            {
                UserInfoAuthorization = request.Headers.Authorization?.ToString();
                return Json("""{ "sub": "user", "name": "Ana Pérez", "email": "ana@example.com", "permissions": [] }""");
            }

            Assert.Equal("Bearer machine-token", request.Headers.Authorization?.ToString());
            Evaluations.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone());
            return Json("""{ "decision": true, "context": { "reason": "granted" } }""");
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
