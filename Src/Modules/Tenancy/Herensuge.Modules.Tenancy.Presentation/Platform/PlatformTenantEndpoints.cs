using Herensuge.Modules.Tenancy.Application.Platform.GetTenants;
using Herensuge.Modules.Tenancy.Application.Platform.SetTenantStatus;

namespace Herensuge.Modules.Tenancy.Presentation.Platform;

public sealed record SetTenantStatusRequest(bool IsActive);

/// <summary>/api/v1/platform/tenants: every organization, for platform administrators (passkey required).</summary>
internal sealed class PlatformTenantEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/platform/tenants")
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin);

        group.MapGet("/", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetTenantsQuery, PagedResponse<PlatformTenantResponse>> handler,
            CancellationToken cancellationToken) =>
            (await handler.Handle(new GetTenantsQuery(search, page ?? 1, pageSize ?? 25), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<PagedResponse<PlatformTenantResponse>>();

        group.MapPatch("/{tenantId:guid}", async (
            Guid tenantId,
            SetTenantStatusRequest request,
            ICommandHandler<SetTenantStatusCommand> handler,
            CancellationToken cancellationToken) =>
            (await handler.Handle(new SetTenantStatusCommand(tenantId, request.IsActive), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}
