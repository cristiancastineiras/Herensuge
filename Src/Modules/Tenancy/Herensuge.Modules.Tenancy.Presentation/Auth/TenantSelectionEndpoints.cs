using System.Security.Claims;
using Herensuge.Modules.Tenancy.Application.Invitations.AcceptInvitation;
using Herensuge.Modules.Tenancy.Application.Invitations.GetInvitationPreview;
using Herensuge.Modules.Tenancy.Application.Tenants.CreateTenant;
using Herensuge.Modules.Tenancy.Application.Tenants.GetUserTenants;

namespace Herensuge.Modules.Tenancy.Presentation.Auth;

public sealed record CreateTenantRequest(string Name);

public sealed record SelectTenantRequest(Guid TenantId);

public sealed record TenantSelectedResponse(Guid TenantId);

/// <summary>
/// Organization picker, onboarding and invitation acceptance in the login SPA (SSO cookie). The
/// chosen organization is stored in the cookie and becomes the tenant_id of the next tokens.
/// </summary>
internal sealed class TenantSelectionEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireCsrfHeader()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapGet("/tenants", async (
            HttpContext httpContext,
            IQueryHandler<GetUserTenantsQuery, IReadOnlyList<UserTenantResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
            if (principal is null)
            {
                return NotSignedIn();
            }

            return (await handler.Handle(new GetUserTenantsQuery(principal.GetUserId()), cancellationToken)).Match(value => Results.Ok(value));
        })
            .Produces<IReadOnlyList<UserTenantResponse>>();

        group.MapPost("/tenants", async (
                CreateTenantRequest request,
                HttpContext httpContext,
                ICommandHandler<CreateTenantCommand, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
                if (principal is null)
                {
                    return NotSignedIn();
                }

                Result<Guid> created = await handler.Handle(new CreateTenantCommand(principal.GetUserId(), request.Name), cancellationToken);
                if (created.IsFailure)
                {
                    return ApiResults.Problem(created);
                }

                await IdentityProviderSession.SetActiveTenantAsync(httpContext, principal, created.Value);
                return Results.Ok(new TenantSelectedResponse(created.Value));
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<TenantSelectedResponse>();

        group.MapPost("/tenants/select", async (
            SelectTenantRequest request,
            HttpContext httpContext,
            IQueryHandler<GetUserTenantsQuery, IReadOnlyList<UserTenantResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
            if (principal is null)
            {
                return NotSignedIn();
            }

            Result<IReadOnlyList<UserTenantResponse>> tenants = await handler.Handle(new GetUserTenantsQuery(principal.GetUserId()), cancellationToken);
            UserTenantResponse? tenant = tenants.IsSuccess ? tenants.Value.FirstOrDefault(t => t.Id == request.TenantId) : null;
            if (tenant is null)
            {
                return ApiResults.Problem(Error.NotFound("Memberships.NotFound", "You are not a member of that organization."));
            }

            if (!tenant.IsActive)
            {
                return ApiResults.Problem(Error.Forbidden("Tenants.Inactive", "The organization is deactivated."));
            }

            await IdentityProviderSession.SetActiveTenantAsync(httpContext, principal, tenant.Id);
            return Results.Ok(new TenantSelectedResponse(tenant.Id));
        })
            .Produces<TenantSelectedResponse>();

        group.MapGet("/invitations/{token}", async (
            string token,
            IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewResponse> handler,
            CancellationToken cancellationToken) =>
            (await handler.Handle(new GetInvitationPreviewQuery(token), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<InvitationPreviewResponse>();

        group.MapPost("/invitations/{token}/accept", async (
                string token,
                HttpContext httpContext,
                ICommandHandler<AcceptInvitationCommand, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
                if (principal is null)
                {
                    return NotSignedIn();
                }

                Result<Guid> accepted = await handler.Handle(new AcceptInvitationCommand(token, principal.GetUserId()), cancellationToken);
                if (accepted.IsFailure)
                {
                    return ApiResults.Problem(accepted);
                }

                await IdentityProviderSession.SetActiveTenantAsync(httpContext, principal, accepted.Value);
                return Results.Ok(new TenantSelectedResponse(accepted.Value));
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<TenantSelectedResponse>();
    }

    private static IResult NotSignedIn() =>
        ApiResults.Problem(Error.Unauthorized("Auth.NotSignedIn", "Sign in first."));
}
