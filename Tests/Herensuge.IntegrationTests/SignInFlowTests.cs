using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.IntegrationTests.Infrastructure;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Notifications.Application.Abstractions;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using OtpNet;

namespace Herensuge.IntegrationTests;

/// <summary>End-to-end flows through the real HTTP pipeline, database, outbox and inbox.</summary>
public sealed partial class SignInFlowTests(HerensugeApiFactory factory) : IClassFixture<HerensugeApiFactory>
{
    private const string Password = "correct horse battery staple";
    private const string Scopes = "openid profile email offline_access herensuge.api";

    [Fact]
    public async Task Registered_user_completes_the_authorization_code_flow_and_calls_the_api()
    {
        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);

        HttpResponseMessage signIn = await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal("signed-in", (await signIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        (string verifier, string challenge) = Pkce();
        string authorize = AuthorizeUrl(challenge);

        // Without an organization the IdP sends the user to onboarding.
        HttpResponseMessage onboarding = await browser.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, onboarding.StatusCode);
        Assert.StartsWith("/onboarding", onboarding.Headers.Location!.OriginalString, StringComparison.Ordinal);

        HttpResponseMessage created = await browser.PostAsJsonAsync("/api/v1/auth/tenants", new { name = "Acme" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        HttpResponseMessage authorized = await browser.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        Uri callback = authorized.Headers.Location!;
        Assert.Equal(HerensugeApiFactory.RedirectUri, callback.GetLeftPart(UriPartial.Path));
        string code = QueryHelpers.ParseQuery(callback.Query)["code"].ToString();

        JsonElement tokens = await RequestTokensAsync(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = HerensugeApiFactory.RedirectUri,
            ["code_verifier"] = verifier,
        });

        HttpClient api = factory.CreateBackchannel();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        JsonElement me = await api.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal(email, me.GetProperty("email").GetString());

        JsonElement refreshed = await RequestTokensAsync(new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!,
        });
        Assert.NotEqual(tokens.GetProperty("refresh_token").GetString(), refreshed.GetProperty("refresh_token").GetString());

        // The registration reached the audit log through outbox → inbox.
        Assert.True(await WaitForAuditAsync("UserRegistered", email));

        // Closing the session cuts its access token at once, even though the token has not expired.
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/v1/auth/sign-out", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            HttpResponseMessage failed = await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "wrong password!!" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        HttpResponseMessage locked = await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal("Users.LockedOut", (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await factory.Messages.WaitForEmailAsync(email, "Cuenta bloqueada temporalmente");
    }

    [Fact]
    public async Task Authenticator_app_becomes_a_required_second_factor()
    {
        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        HttpClient earlierBrowser = factory.CreateBrowser();
        Assert.Equal(HttpStatusCode.OK, (await earlierBrowser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);

        JsonElement setup = await (await browser.PostAsync("/api/v1/auth/mfa/totp", null)).Content.ReadFromJsonAsync<JsonElement>();
        var totp = new Totp(Base32Encoding.ToBytes(setup.GetProperty("sharedKey").GetString()));
        HttpResponseMessage confirmed = await browser.PostAsJsonAsync("/api/v1/auth/mfa/totp/confirm", new { code = totp.ComputeTotp() });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(10, (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes").GetArrayLength());

        // Turning MFA on closes the sessions opened without it, and keeps the one that turned it on.
        Assert.False((await earlierBrowser.GetFromJsonAsync<JsonElement>("/api/v1/auth/session")).GetProperty("signedIn").GetBoolean());
        Assert.True((await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/session")).GetProperty("signedIn").GetBoolean());

        HttpClient otherBrowser = factory.CreateBrowser();
        JsonElement firstFactor = await (await otherBrowser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mfa-required", firstFactor.GetProperty("status").GetString());

        // The code of the next time step: the current one was already used to confirm the setup.
        string nextCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30));
        HttpResponseMessage secondFactor = await otherBrowser.PostAsJsonAsync("/api/v1/auth/sign-in/mfa", new { method = "totp", code = nextCode });
        Assert.Equal(HttpStatusCode.OK, secondFactor.StatusCode);
        Assert.Equal("signed-in", (await secondFactor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // Without the app at hand: a code sent to the verified email also answers the challenge.
        HttpClient emailBrowser = factory.CreateBrowser();
        JsonElement challenge = await (await emailBrowser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("email", challenge.GetProperty("mfaMethods").EnumerateArray().Select(method => method.GetString()));
        Assert.Equal(HttpStatusCode.Accepted, (await emailBrowser.PostAsync("/api/v1/auth/sign-in/mfa/email", null)).StatusCode);

        EmailMessage codeEmail = await factory.Messages.WaitForEmailAsync(email, "Tu código de acceso");
        string emailCode = SixDigitCodePattern().Match(codeEmail.TextBody).Value;
        HttpResponseMessage byEmail = await emailBrowser.PostAsJsonAsync("/api/v1/auth/sign-in/mfa", new { method = "email", code = emailCode });
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
    }

    [Fact]
    public async Task Application_permissions_reach_its_token_and_the_decision_point()
    {
        // The application: its OIDC client (BFF + backend) and its manifest, synced as a platform administrator would.
        const string ClientId = "facturas-app";
        const string RedirectUri = "https://facturas.test/bff/callback";
        string secret;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            secret = (await scope.ServiceProvider.GetRequiredService<IClientRegistry>().CreateAsync(
                new ClientDefinition(ClientId, true, new ClientSettings("Facturas", [RedirectUri], [], ["openid", "offline_access", "facturas.api", "herensuge.access"])),
                CancellationToken.None)).Value!;

            Result<ManifestSyncResponse> synced = await scope.ServiceProvider.GetRequiredService<ICommandHandler<SyncManifestCommand, ManifestSyncResponse>>().Handle(
                new SyncManifestCommand(
                    new AccessManifest(
                        "facturas",
                        "Facturación",
                        ClientId,
                        null,
                        [new ManifestPermission("invoice:read", null, null), new ManifestPermission("invoice:approve", null, "mfa")],
                        [new ManifestRole("finance", "Finanzas", null, ["invoice:*"], ["owner"])]),
                    CallerClientId: null),
                CancellationToken.None);
            Assert.True(synced.IsSuccess);
        }

        Assert.True(await WaitForScopeAsync("facturas.api"));

        // An organization owner signs in to the application.
        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/tenants", new { name = "Acme" })).StatusCode);

        (string verifier, string challenge) = Pkce();
        HttpResponseMessage authorized = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "openid offline_access facturas.api",
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }));
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        string code = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();

        JsonElement tokens = await PostTokenAsync(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier,
            ["client_id"] = ClientId,
            ["client_secret"] = secret,
        });

        // One token, one API: aud = facturas-api, with the roles and permissions of the owner there.
        JsonElement claims = JwtPayload(tokens.GetProperty("access_token").GetString()!);
        Assert.Contains("facturas-api", Values(claims, "aud"));
        Assert.Equal(["finance"], Values(claims, "roles"));
        Assert.Equal(["invoice:approve", "invoice:read"], Values(claims, "permissions").Order());

        // The application's backend asks the decision point with its own token (client_credentials).
        JsonElement machine = await PostTokenAsync(new()
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "herensuge.access",
            ["client_id"] = ClientId,
            ["client_secret"] = secret,
        });
        HttpClient backend = factory.CreateBackchannel();
        backend.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", machine.GetProperty("access_token").GetString());

        object Evaluation(string action) => new
        {
            subject = new
            {
                type = "user",
                id = claims.GetProperty("sub").GetString(),
                properties = new { tenant_id = claims.GetProperty("tenant_id").GetString(), sid = claims.GetProperty("sid").GetString(), amr = Values(claims, "amr") },
            },
            action = new { name = action },
            resource = new { type = "invoice", id = "inv-1" },
        };

        JsonElement read = await (await backend.PostAsJsonAsync("/access/v1/evaluation", Evaluation("read"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(read.GetProperty("decision").GetBoolean());

        // invoice:approve requires MFA and the owner signed in with a password only.
        JsonElement approve = await (await backend.PostAsJsonAsync("/access/v1/evaluation", Evaluation("approve"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(approve.GetProperty("decision").GetBoolean());
        Assert.Equal("step_up_required", approve.GetProperty("context").GetProperty("reason").GetString());

        // The machine token cannot call the user APIs.
        Assert.Equal(HttpStatusCode.Forbidden, (await backend.GetAsync("/api/v1/me")).StatusCode);
    }

    private async Task<bool> WaitForScopeAsync(string name)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>().FindByNameAsync(name) is not null)
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }

    private async Task<JsonElement> PostTokenAsync(Dictionary<string, string> parameters)
    {
        HttpResponseMessage response = await factory.CreateBackchannel().PostAsync("/connect/token", new FormUrlEncodedContent(parameters));
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static JsonElement JwtPayload(string jwt) =>
        JsonDocument.Parse(Base64UrlTextEncoder.Decode(jwt.Split('.')[1])).RootElement.Clone();

    /// <summary>A JWT claim is a string with one value and an array with several.</summary>
    private static string[] Values(JsonElement claims, string name) =>
        !claims.TryGetProperty(name, out JsonElement value) ? []
        : value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray().Select(item => item.GetString()!)]
        : [value.GetString()!];

    private async Task RegisterAndVerifyAsync(HttpClient browser, string email)
    {
        HttpResponseMessage registered = await browser.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Test User" });
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);

        EmailMessage verification = await factory.Messages.WaitForEmailAsync(email, "Verifica tu email");
        string token = Uri.UnescapeDataString(TokenPattern().Match(verification.TextBody).Groups[1].Value);

        HttpResponseMessage verified = await browser.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    private async Task<JsonElement> RequestTokensAsync(Dictionary<string, string> parameters)
    {
        parameters["client_id"] = HerensugeApiFactory.ClientId;
        parameters["client_secret"] = HerensugeApiFactory.ClientSecret;

        HttpResponseMessage response = await factory.CreateBackchannel().PostAsync("/connect/token", new FormUrlEncodedContent(parameters));
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<bool> WaitForAuditAsync(string action, string email)
    {
        NpgsqlDataSource dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
            long count = await connection.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM audit.audit_entries WHERE action = @Action AND metadata->>'email' = @Email",
                new { Action = action, Email = email });
            if (count > 0)
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }

    private static string AuthorizeUrl(string challenge) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = HerensugeApiFactory.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = HerensugeApiFactory.RedirectUri,
            ["scope"] = Scopes,
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        });

    private static (string Verifier, string Challenge) Pkce()
    {
        string verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigitCodePattern();
}
