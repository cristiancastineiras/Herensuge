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

    /// <summary>Plan D11: what a support session (or a delegated token) cannot do.</summary>
    public static readonly Error SupportReadOnly = Error.Forbidden("Support.ReadOnly", "A support session is read only.");

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
            // A support session (act) never passes: it is read only and the administrator never signed in as the user.
            if (context.HttpContext.User.GetActor() is not null)
            {
                return ApiResults.Problem(SupportReadOnly);
            }

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

    /// <summary>
    /// The access token must carry <paramref name="permission"/> (Herensuge's own permissions, plan D1). A quick check
    /// for screens and reads: commands decide again against the database through Access.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            System.Security.Claims.ClaimsPrincipal user = context.HttpContext.User;
            IReadOnlyList<string> values = user.GetPermissions();
            bool granted = values.Contains(permission, StringComparer.Ordinal);

            // shortcut: tokens issued before Herensuge's permissions existed carry none; trust the owner/admin role until they
            // expire (5 min). Remove after the first deployment of plan WP1 (plan WP7.2).
            if (!granted && values.Count == 0)
            {
                string? role = user.FindFirst(Application.Security.HerensugeClaimTypes.TenantRole)?.Value;
                granted = role is "owner" or "admin";
            }

            if (!granted)
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

/// <summary>Endpoint metadata: the endpoint may change state during a support session (plan D11; none does today).</summary>
public sealed class AllowDuringSupportMetadata;

/// <summary>
/// Plan D11: requests with an actor (act: support session or delegated token) cannot change anything. Every POST, PUT,
/// PATCH and DELETE of a principal with act is refused with 403 Support.ReadOnly, except on endpoints marked with
/// <see cref="SupportSessionGuard.AllowDuringSupport{TBuilder}"/>. Runs after authentication and authorization.
/// </summary>
public static class SupportSessionGuard
{
    public static TBuilder AllowDuringSupport<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowDuringSupportMetadata());

    /// <summary>For the IdP endpoints that work with the cookie (MFA set-up, invitations, new organizations).</summary>
    public static TBuilder RejectDuringSupport<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
            (await IdentityProviderSession.AuthenticateAsync(context.HttpContext))?.GetActor() is not null
                ? ApiResults.Problem(EndpointFilters.SupportReadOnly)
                : await next(context));

    public static IApplicationBuilder UseSupportSessionGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            HttpRequest request = context.Request;
            bool safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
            if (!safe
                && context.User.GetActor() is not null
                && context.GetEndpoint()?.Metadata.GetMetadata<AllowDuringSupportMetadata>() is null)
            {
                await ApiResults.Problem(EndpointFilters.SupportReadOnly).ExecuteAsync(context);
                return;
            }

            await next(context);
        });
}
