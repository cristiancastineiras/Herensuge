using Herensuge.Bff;
using Herensuge.Bff.Endpoints;
using Herensuge.Bff.Hosting;
using Herensuge.Bff.Sessions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

BffOptions bff = builder.Configuration.GetSection(BffOptions.SectionName).Get<BffOptions>() ?? new BffOptions();
builder.Services.AddOptions<BffOptions>().Bind(builder.Configuration.GetSection(BffOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);

builder.AddBffTelemetry();
builder.Services.AddBffHosting(builder.Configuration, builder.Environment);
builder.Services.AddBffState(builder.Configuration, builder.Environment);
builder.Services.AddBffAuthentication(bff);
builder.Services.AddBffProxy(builder.Configuration);
builder.Services.AddAuthorization();

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseBffSecurityHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapBffEndpoints();

// Every /api call must carry X-CSRF: 1 and a live session; the access token is attached by the transform.
app.MapReverseProxy(proxy => proxy.Use(async (context, next) =>
{
    if (context.Request.Headers[BffDefaults.CsrfHeader] != "1")
    {
        await Results.Problem(
            title: "The X-CSRF header is required.",
            statusCode: StatusCodes.Status403Forbidden,
            extensions: new Dictionary<string, object?> { ["code"] = "Security.CsrfHeaderMissing" }).ExecuteAsync(context);
        return;
    }

    string? accessToken = await context.RequestServices.GetRequiredService<AccessTokenProvider>().GetAccessTokenAsync(context);
    if (accessToken is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    context.Items[BffDefaults.AccessToken] = accessToken;
    await next();
}));

app.MapBffSpa();

await app.RunAsync();
