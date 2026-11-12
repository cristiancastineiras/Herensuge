using Herensuge.Api.Hosting;
using Herensuge.Common.Application;
using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Web;
using Herensuge.Common.Presentation.Endpoints;
using Herensuge.Modules.Audit.Infrastructure;
using Herensuge.Modules.Identity.Infrastructure;
using Herensuge.Modules.Notifications.Infrastructure;
using Herensuge.Modules.Oidc.Infrastructure;
using Herensuge.Modules.Tenancy.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddHerensugeTelemetry("herensuge-api");
builder.Services.AddHerensugeForwardedHeaders(builder.Configuration);
builder.Services.AddHerensugeProblemDetails();
builder.Services.AddOpenApi();

// Registered first: hosted services start in order, and the module seeders and jobs need the schema.
builder.Services.AddStartupMigrations(builder.Configuration);

builder.Services.AddCommonApplication(ModuleAssemblies.Application);
builder.Services.AddCommonInfrastructure(builder.Configuration, builder.Environment);
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddTenancyModule()
    .AddOidcModule(builder.Configuration, builder.Environment)
    .AddAuditModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration, builder.Environment);
builder.Services.AddEndpoints(ModuleAssemblies.Presentation);

builder.Services.AddHerensugeDataProtection(builder.Configuration);
builder.Services.AddHerensugeRateLimiting(builder.Configuration);
builder.Services.AddSpaHosting(builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

// `dotnet Herensuge.Api.dll --migrate` applies every module migration and exits (deployment step).
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await app.Services.MigrateDatabasesAsync();
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHerensugeSecurityHeaders();
app.UseSpaStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHerensugeHealthChecks();
app.MapEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSpa();

await app.RunAsync();

/// <summary>Entry point, public for WebApplicationFactory in the integration tests.</summary>
public partial class Program
{
}
