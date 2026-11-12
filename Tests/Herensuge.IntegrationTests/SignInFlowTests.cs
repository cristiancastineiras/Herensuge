using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Herensuge.IntegrationTests.Infrastructure;
using Herensuge.Modules.Notifications.Application.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

        JsonElement setup = await (await browser.PostAsync("/api/v1/auth/mfa/totp", null)).Content.ReadFromJsonAsync<JsonElement>();
        var totp = new Totp(Base32Encoding.ToBytes(setup.GetProperty("sharedKey").GetString()));
        HttpResponseMessage confirmed = await browser.PostAsJsonAsync("/api/v1/auth/mfa/totp/confirm", new { code = totp.ComputeTotp() });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(10, (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes").GetArrayLength());

        HttpClient otherBrowser = factory.CreateBrowser();
        JsonElement firstFactor = await (await otherBrowser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mfa-required", firstFactor.GetProperty("status").GetString());

        // The code of the next time step: the current one was already used to confirm the setup.
        string nextCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30));
        HttpResponseMessage secondFactor = await otherBrowser.PostAsJsonAsync("/api/v1/auth/sign-in/mfa", new { method = "totp", code = nextCode });
        Assert.Equal(HttpStatusCode.OK, secondFactor.StatusCode);
        Assert.Equal("signed-in", (await secondFactor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

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
}
