using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Herensuge.Common.Domain;
using Herensuge.IntegrationTests.Infrastructure;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Contracts;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.IntegrationTests;

/// <summary>Login page of an application: extra registration data, accepted sign-in methods and closed registration.</summary>
public sealed partial class SignInFlowTests
{
    private static readonly ProfileField Company = new("company", "Empresa", ProfileFieldTypes.Text, Required: true, []);
    private static readonly ProfileField Size = new("size", "Tamaño", ProfileFieldTypes.Select, Required: false, ["1-10", "11-50"]);

    [Fact]
    public async Task An_application_asks_for_its_own_data_and_methods_before_issuing_a_code()
    {
        string clientId = $"shop-{Guid.NewGuid():N}"[..20];
        string secret = await CreateClientAsync(clientId);
        LoginSettings settings = LoginSettings.Default with { ProfileFields = [Company, Size] };
        await UpdateLoginSettingsAsync(clientId, settings);

        // The login SPA reads the page anonymously.
        HttpClient browser = factory.CreateBrowser();
        JsonElement page = await browser.GetFromJsonAsync<JsonElement>($"/api/v1/auth/clients/{clientId}/login-page");
        Assert.Equal(2, page.GetProperty("settings").GetProperty("profileFields").GetArrayLength());

        // Registering from its page needs its required fields.
        HttpResponseMessage incomplete = await browser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ana", clientId, profileFields = new { size = "1-10" } });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal("Users.ProfileFieldRequired", (await incomplete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // Someone who registered elsewhere (here: without a client) is asked for the data before getting a code.
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/tenants", new { name = "Acme" })).StatusCode);

        (string verifier, string challenge) = Pkce();
        string authorize = AuthorizeUrl(clientId, challenge);
        HttpResponseMessage completeProfile = await browser.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, completeProfile.StatusCode);
        Assert.StartsWith("/complete-profile?returnUrl=", completeProfile.Headers.Location!.OriginalString, StringComparison.Ordinal);

        HttpResponseMessage saved = await browser.PutAsJsonAsync(
            "/api/v1/auth/profile-fields",
            new { clientId, values = new { company = "Acme", size = "11-50", salary = "ignored" } });
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        // Now the code is issued and the ID token carries the application's fields (and nothing else).
        HttpResponseMessage authorized = await browser.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        string code = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();
        JsonElement tokens = await PostTokenAsync(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = HerensugeApiFactory.RedirectUri,
            ["code_verifier"] = verifier,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });
        JsonElement profileFields = JwtPayload(tokens.GetProperty("id_token").GetString()!).GetProperty("profile_fields");
        Assert.Equal("Acme", profileFields.GetProperty("company").GetString());
        Assert.Equal("11-50", profileFields.GetProperty("size").GetString());
        Assert.False(profileFields.TryGetProperty("salary", out _));

        // The application stops accepting passwords: this password session has to sign in again with another method.
        await UpdateLoginSettingsAsync(clientId, settings with { Methods = new LoginMethods(Password: false, Passkey: true, Google: false, Microsoft: false) });
        HttpResponseMessage otherMethod = await browser.GetAsync(AuthorizeUrl(clientId, Pkce().Challenge));
        Assert.Equal(HttpStatusCode.Redirect, otherMethod.StatusCode);
        Assert.StartsWith("/login?", otherMethod.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("error=sign_in_method_not_allowed", otherMethod.Headers.Location!.OriginalString, StringComparison.Ordinal);

        // And with registration closed nobody signs up from its page.
        await UpdateLoginSettingsAsync(clientId, settings with { AllowRegistration = false });
        HttpResponseMessage closed = await browser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ana", clientId, profileFields = new { company = "Acme" } });
        Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);
        Assert.Equal("Users.RegistrationDisabled", (await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private async Task<string> CreateClientAsync(string clientId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        Result<string?> created = await scope.ServiceProvider.GetRequiredService<IClientRegistry>().CreateAsync(
            new ClientDefinition(clientId, IsConfidential: true, new ClientSettings(clientId, [HerensugeApiFactory.RedirectUri], [], Scopes.Split(' '))),
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        return created.Value!;
    }

    private async Task UpdateLoginSettingsAsync(string clientId, LoginSettings settings)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        Result updated = await scope.ServiceProvider.GetRequiredService<IClientRegistry>().UpdateLoginSettingsAsync(clientId, settings, CancellationToken.None);
        Assert.True(updated.IsSuccess);
    }

    private static string AuthorizeUrl(string clientId, string challenge) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = HerensugeApiFactory.RedirectUri,
            ["scope"] = Scopes,
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        });
}
