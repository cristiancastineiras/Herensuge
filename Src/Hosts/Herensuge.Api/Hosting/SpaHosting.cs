namespace Herensuge.Api.Hosting;

/// <summary>
/// Serves the login SPA from the IdP origin. Production: the built files in wwwroot. Development:
/// every non-API route is forwarded to the Vite dev server (Spa:DevServerUrl), so the browser keeps
/// the same origin, cookies and redirects as in production.
/// </summary>
internal static class SpaHosting
{
    private static readonly string[] BackendPrefixes = ["/api", "/connect", "/.well-known", "/health", "/openapi"];

    public static IServiceCollection AddSpaHosting(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration["Spa:DevServerUrl"]))
        {
            services.AddHttpForwarder();
        }

        return services;
    }

    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        app.UseStaticFiles();
        return app;
    }

    public static WebApplication MapSpa(this WebApplication app)
    {
        string? devServerUrl = app.Configuration["Spa:DevServerUrl"];
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(devServerUrl))
        {
            app.MapForwarder("/{**catch-all}", devServerUrl);
            return app;
        }

        string index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
        if (!File.Exists(index))
        {
            return app;
        }

        app.MapFallback(async context =>
        {
            if (BackendPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index);
        });

        return app;
    }
}
