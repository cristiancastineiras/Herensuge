using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.IntegrationTests;

public sealed partial class SignInFlowTests
{
    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";

    /// <summary>Plan D14: A trades its user's token for one of B, with act; nothing else can be exchanged.</summary>
    [Fact]
    public async Task An_application_exchanges_its_users_token_for_another_application_only_once()
    {
        string almacen = await RegisterApplicationAsync("almacen");
        string ventas = await RegisterApplicationAsync("ventas");

        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/tenants", new { name = "Acme" })).StatusCode);

        string userToken = await AuthorizationCodeTokenAsync(browser, "almacen-app", almacen, "almacen.api");
        Dictionary<string, string> Exchange(string subjectToken, string clientId, string secret, string scope) => new()
        {
            ["grant_type"] = TokenExchangeGrant,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["scope"] = scope,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        };

        // A → B: same user and session, B's audience and permissions, and A as the actor.
        JsonElement exchanged = await PostTokenAsync(Exchange(userToken, "almacen-app", almacen, "ventas.api"));
        Assert.False(exchanged.TryGetProperty("refresh_token", out _));
        JsonElement claims = JwtPayload(exchanged.GetProperty("access_token").GetString()!);
        JsonElement original = JwtPayload(userToken);
        Assert.Contains("ventas-api", Values(claims, "aud"));
        Assert.Equal(original.GetProperty("sub").GetString(), claims.GetProperty("sub").GetString());
        Assert.Equal(original.GetProperty("sid").GetString(), claims.GetProperty("sid").GetString());
        Assert.Equal("almacen-app", claims.GetProperty("act").GetProperty("sub").GetString());
        Assert.Equal(["ventas:read"], Values(claims, "permissions"));

        // B cannot trade A's tokens, and an exchanged token cannot be exchanged again.
        HttpClient backchannel = factory.CreateBackchannel();
        HttpResponseMessage foreign = await backchannel.PostAsync("/connect/token", new FormUrlEncodedContent(Exchange(userToken, "ventas-app", ventas, "almacen.api")));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);

        HttpResponseMessage chained = await backchannel.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(Exchange(exchanged.GetProperty("access_token").GetString()!, "ventas-app", ventas, "almacen.api")));
        Assert.Equal(HttpStatusCode.BadRequest, chained.StatusCode);
    }

    /// <summary>A confidential client and the manifest of "&lt;key&gt;" (role granted to owners). Returns the secret.</summary>
    private async Task<string> RegisterApplicationAsync(string key)
    {
        string secret;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            secret = (await scope.ServiceProvider.GetRequiredService<IClientRegistry>().CreateAsync(
                new ClientDefinition(
                    $"{key}-app",
                    true,
                    new ClientSettings(key, [$"https://{key}.test/bff/callback"], [], ["openid", "offline_access", $"{key}.api", "herensuge.access"])),
                CancellationToken.None)).Value!;

            Result<ManifestSyncResponse> synced = await scope.ServiceProvider.GetRequiredService<ICommandHandler<SyncManifestCommand, ManifestSyncResponse>>().Handle(
                new SyncManifestCommand(
                    new AccessManifest(key, key, $"{key}-app", null, [new ManifestPermission($"{key}:read", null, null)], [new ManifestRole("reader", "Lectura", null, ["*"], ["owner"])]),
                    CallerClientId: null),
                CancellationToken.None);
            Assert.True(synced.IsSuccess);
        }

        Assert.True(await WaitForScopeAsync($"{key}.api"));
        return secret;
    }

    private async Task<string> AuthorizationCodeTokenAsync(HttpClient browser, string clientId, string secret, string scope)
    {
        string redirectUri = $"https://{clientId[..clientId.IndexOf('-', StringComparison.Ordinal)]}.test/bff/callback";
        (string verifier, string challenge) = Pkce();
        HttpResponseMessage authorized = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = $"openid offline_access {scope}",
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }));
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);

        JsonElement tokens = await PostTokenAsync(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString(),
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });
        return tokens.GetProperty("access_token").GetString()!;
    }
}
