using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Application.Assignments;
using Herensuge.Modules.Access.Application.Plans;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Application.ServiceAccounts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Tenancy.Application.Platform.SetTenantStatus;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.IntegrationTests;

public sealed partial class SignInFlowTests
{
    /// <summary>Plan D5 (plans), D7 (service accounts) and the client subject of the decision point.</summary>
    [Fact]
    public async Task A_service_account_gets_its_roles_within_its_plan_while_its_organization_is_active()
    {
        // An application with a contractable feature ("payroll/approvals").
        const string ClientId = "nominas-app";
        string appSecret;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            appSecret = (await scope.ServiceProvider.GetRequiredService<IClientRegistry>().CreateAsync(
                new ClientDefinition(ClientId, true, new ClientSettings("Nóminas", ["https://nominas.test/bff/callback"], [], ["openid", "nominas.api", "herensuge.access"])),
                CancellationToken.None)).Value!;
            Result<ManifestSyncResponse> synced = await Handler<SyncManifestCommand, ManifestSyncResponse>(scope).Handle(
                new SyncManifestCommand(
                    new AccessManifest(
                        "nominas",
                        "Nóminas",
                        ClientId,
                        null,
                        [new ManifestPermission("payroll:read", null, null), new ManifestPermission("payroll:approve", null, null, Feature: "approvals")],
                        [new ManifestRole("payroll", "Nóminas", null, ["payroll:*"], [])],
                        [new ManifestFeature("approvals", "Aprobaciones")]),
                    CallerClientId: null),
                CancellationToken.None);
            Assert.True(synced.IsSuccess);
        }

        Assert.True(await WaitForScopeAsync("nominas.api"));

        // An owner creates the organization, a service account and gives it the application's role.
        HttpClient browser = factory.CreateBrowser();
        string email = UniqueEmail();
        await RegisterAndVerifyAsync(browser, email);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/tenants", new { name = "Acme" })).StatusCode);
        JsonElement session = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");
        Guid owner = session.GetProperty("userId").GetGuid();
        Guid tenantId = session.GetProperty("tenantId").GetGuid();

        ServiceAccountCreatedResponse account;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            account = (await Handler<CreateServiceAccountCommand, ServiceAccountCreatedResponse>(scope)
                .Handle(new CreateServiceAccountCommand(tenantId, owner, "ci-bot"), CancellationToken.None)).Value;

            ResourceServer nominas = (await scope.ServiceProvider.GetRequiredService<IResourceServerRepository>().GetByKeyAsync("nominas", CancellationToken.None))!;
            Result assigned = await scope.ServiceProvider.GetRequiredService<ICommandHandler<SetRolesCommand>>().Handle(
                new SetRolesCommand(tenantId, owner, SubjectTypes.Client, account.Id, [new RoleGrant(null, nominas.RoleTemplates.Single().Id, null)]),
                CancellationToken.None);
            Assert.True(assigned.IsSuccess);
        }

        Dictionary<string, string> machine = new()
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "nominas.api",
            ["client_id"] = account.ClientId,
            ["client_secret"] = account.ClientSecret,
        };

        // Without plans everything is included.
        JsonElement claims = JwtPayload((await PostTokenAsync(machine)).GetProperty("access_token").GetString()!);
        Assert.Equal(tenantId.ToString(), claims.GetProperty("tenant_id").GetString());
        Assert.Contains("nominas-api", Values(claims, "aud"));
        Assert.Equal(["payroll:approve", "payroll:read"], Values(claims, "permissions").Order());

        // A default plan without the feature: approvals are no longer granted, in the token or at the decision point.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<ICommandHandler<CreatePlanCommand>>()
                .Handle(new CreatePlanCommand("basic-test", "Basic", []), CancellationToken.None)).IsSuccess);
        }

        try
        {
            claims = JwtPayload((await PostTokenAsync(machine)).GetProperty("access_token").GetString()!);
            Assert.Equal(["payroll:read"], Values(claims, "permissions"));

            HttpClient backend = factory.CreateBackchannel();
            JsonElement appToken = await PostTokenAsync(new()
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "herensuge.access",
                ["client_id"] = ClientId,
                ["client_secret"] = appSecret,
            });
            backend.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appToken.GetProperty("access_token").GetString());
            JsonElement decision = await (await backend.PostAsJsonAsync("/access/v1/evaluation", new
            {
                subject = new { type = "client", id = account.ClientId, properties = new { tenant_id = tenantId } },
                action = new { name = "approve" },
                resource = new { type = "payroll", id = "2026-11" },
            })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(decision.GetProperty("decision").GetBoolean());
            Assert.Equal("not_entitled", decision.GetProperty("context").GetProperty("reason").GetString());
        }
        finally
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<DeletePlanCommand>>().Handle(new DeletePlanCommand("basic-test"), CancellationToken.None);
        }

        // A suspended organization's service accounts get no tokens.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<SetTenantStatusCommand>>()
                .Handle(new SetTenantStatusCommand(tenantId, IsActive: false), CancellationToken.None);
        }

        HttpResponseMessage refused = await factory.CreateBackchannel().PostAsync("/connect/token", new FormUrlEncodedContent(machine));
        Assert.False(refused.IsSuccessStatusCode);
    }

    private static ICommandHandler<TCommand, TResponse> Handler<TCommand, TResponse>(AsyncServiceScope scope)
        where TCommand : ICommand<TResponse> =>
        scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
}
