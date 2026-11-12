namespace Herensuge.Bff.Hosting;

internal static class BffApplicationExtensions
{
    private static readonly string[] BackendPrefixes = ["/api", "/bff", "/health"];

    public static WebApplication UseBffSecurityHeaders(this WebApplication app)
    {
        // Development with the Vite dev server (Spa:DevServerUrl) only: Vite injects the React Refresh
        // preamble and the CSS as inline code. Production keeps 'self' only.
        bool viteDevServer = app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(app.Configuration["Spa:DevServerUrl"]);
        string inline = viteDevServer ? " 'unsafe-inline'" : string.Empty;
        string contentSecurityPolicy =
            $"default-src 'self'; script-src 'self'{inline}; style-src 'self'{inline}; img-src 'self' data:; font-src 'self'; " +
            "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers.ContentSecurityPolicy = contentSecurityPolicy;
                return Task.CompletedTask;
            });
            await next();
        });

        return app;
    }

    /// <summary>The portal SPA: wwwroot in production, the Vite dev server (Spa:DevServerUrl) in development.</summary>
    public static WebApplication MapBffSpa(this WebApplication app)
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
