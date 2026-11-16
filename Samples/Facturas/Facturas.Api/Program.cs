using System.Security.Claims;
using Herensuge.Access.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;

// Facturas: the API of a SaaS application protected by Herensuge. Its BFF (a Herensuge.Bff with
// bff.appsettings.json) signs users in and forwards /api/* here with the user's access token.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHerensugeAccess(options => builder.Configuration.GetSection("Herensuge").Bind(options));

// The client IP reaches the decision point (trusted networks): trust only the BFF's X-Forwarded-For (see README).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = builder.Configuration.GetValue("Herensuge:ProxyHops", 1);
});

WebApplication app = builder.Build();
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

Invoice[] invoices =
[
    new("inv-1", "Acme", 1200m, Approved: false),
    new("inv-2", "Globex", 340.5m, Approved: true),
];

RouteGroupBuilder api = app.MapGroup("/api/invoices");

// In the token's "permissions": allowed by the SDK without a call to Herensuge.
api.MapGet("/", () => invoices).RequirePermission("invoice:read");

// "requires": "mfa" in the manifest: always decided by the decision point. Without MFA → 401 with the RFC 9470
// step-up challenge, and the SPA calls reauthenticate() from @herensuge/react.
api.MapPost("/{id}/approve", (string id) =>
    invoices.FirstOrDefault(invoice => invoice.Id == id) is { } invoice
        ? Results.Ok(invoice with { Approved = true })
        : Results.NotFound())
    .RequirePermission("invoice:approve");

// A check in code, on one object (the PDP receives resource.id).
api.MapDelete("/{id}", async (string id, ClaimsPrincipal user, HttpContext http, IHerensugeAccess access) =>
{
    AccessCheckResult check = await access.CheckAsync(user, "invoice:delete", id, http.Connection.RemoteIpAddress, http.RequestAborted);
    return check.Allowed ? Results.NoContent() : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: check.Reason);
}).RequireAuthorization();

app.Run();

internal sealed record Invoice(string Id, string Customer, decimal Amount, bool Approved);
