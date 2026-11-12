using Herensuge.Common.Domain;
using Herensuge.Common.Presentation.Results;
using Herensuge.Common.Presentation.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Common.Presentation.Filters;

public static class EndpointFilters
{
    public const string CsrfHeaderName = "X-CSRF";

    public static readonly TimeSpan RecentAuthenticationWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Requests that change state must carry "X-CSRF: 1". Browsers cannot add custom headers to
    /// cross-site forms, and cross-origin fetch would need CORS, which is never enabled.
    /// </summary>
    public static TBuilder RequireCsrfHeader<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            HttpRequest request = context.HttpContext.Request;
            bool safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
            if (!safe && request.Headers[CsrfHeaderName] != "1")
            {
                return ApiResults.Problem(Error.Forbidden("Security.CsrfHeaderMissing", "The X-CSRF header is required."));
            }

            return await next(context);
        });

    /// <summary>Sensitive operations need a sign-in younger than <see cref="RecentAuthenticationWindow"/>.</summary>
    public static TBuilder RequireRecentAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            DateTimeOffset now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            DateTimeOffset? authenticatedAt = context.HttpContext.User.GetAuthenticationTime();
            if (authenticatedAt is null || now - authenticatedAt.Value > RecentAuthenticationWindow)
            {
                return Microsoft.AspNetCore.Http.Results.Problem(
                    title: "This operation requires a recent sign-in.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "Auth.ReauthenticationRequired",
                        ["error"] = "reauth_required",
                    });
            }

            return await next(context);
        });

    /// <summary>
    /// Read-only organization screens need an owner or admin access token (tenant_role claim). Changes
    /// are checked again against the database by the command handlers.
    /// </summary>
    public static TBuilder RequireTenantAdministrator<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            string? role = context.HttpContext.User.FindFirst(Application.Security.HerensugeClaimTypes.TenantRole)?.Value;
            if (role is not ("owner" or "admin"))
            {
                return ApiResults.Problem(Error.Forbidden("Memberships.NotAllowed", "Your role does not allow this operation."));
            }

            return await next(context);
        });

    /// <summary>The {tenantId} route value must be the organization the access token was issued for.</summary>
    public static TBuilder RequireActiveTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            object? routeValue = context.HttpContext.Request.RouteValues["tenantId"];
            Guid? tokenTenant = context.HttpContext.User.GetTenantId();
            if (tokenTenant is null || !Guid.TryParse(routeValue?.ToString(), out Guid routeTenant) || routeTenant != tokenTenant)
            {
                return ApiResults.Problem(Error.Forbidden(
                    "Auth.TenantMismatch",
                    "The access token was issued for a different organization."));
            }

            return await next(context);
        });
}
