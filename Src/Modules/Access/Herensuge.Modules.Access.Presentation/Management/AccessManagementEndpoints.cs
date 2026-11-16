using System.Security.Claims;
using Herensuge.Modules.Access.Application.Assignments;
using Herensuge.Modules.Access.Application.Catalog.GetAccessCatalog;
using Herensuge.Modules.Access.Application.ResourceServers.GetResourceServers;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Application.Roles;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.Presentation.Management;

public sealed record RoleRequest(string Name, string? Description, IReadOnlyList<Guid> PermissionIds);

public sealed record RoleCreatedResponse(Guid Id);

public sealed record SetMemberRolesRequest(IReadOnlyList<RoleGrant> Roles);

/// <summary>
/// Platform: applications and their manifests. Organization (owner/admin): what can be granted, roles and the roles
/// of each member. Bearer tokens through the portal BFF.
/// </summary>
internal sealed class AccessManagementEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder platform = app.MapGroup("/api/v1/platform/resource-servers")
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin);

        platform.MapGet("/", async (IQueryHandler<GetResourceServersQuery, IReadOnlyList<ResourceServerResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetResourceServersQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<IReadOnlyList<ResourceServerResponse>>();

        platform.MapPut("/{key}/manifest", async (string key, AccessManifest manifest, ICommandHandler<SyncManifestCommand, ManifestSyncResponse> handler, CancellationToken cancellationToken) =>
            key != manifest.ResourceServer
                ? ApiResults.Problem(Error.Failure("Access.InvalidManifest", "The key in the URL and resourceServer in the manifest must match."))
                : (await handler.Handle(new SyncManifestCommand(manifest, CallerClientId: null), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<ManifestSyncResponse>();

        RouteGroupBuilder tenant = app.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Organization")
            .RequireAuthorization(HerensugePolicies.Api)
            .RequireActiveTenant();

        // tenantId is declared so the OpenAPI contract has the path parameter; the catalog is the same for every tenant.
        tenant.MapGet("/permissions", async (Guid tenantId, IQueryHandler<GetAccessCatalogQuery, IReadOnlyList<ApplicationCatalogResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetAccessCatalogQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageRoles)
            .Produces<IReadOnlyList<ApplicationCatalogResponse>>();

        tenant.MapGet("/roles", async (Guid tenantId, IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetRolesQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageRoles)
            .Produces<IReadOnlyList<RoleResponse>>();

        tenant.MapPost("/roles", async (Guid tenantId, RoleRequest request, ClaimsPrincipal user, ICommandHandler<CreateRoleCommand, Guid> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new CreateRoleCommand(tenantId, user.GetUserId(), request.Name, request.Description, request.PermissionIds ?? []), cancellationToken))
                .Match(id => Results.Ok(new RoleCreatedResponse(id))))
            .Produces<RoleCreatedResponse>();

        tenant.MapPatch("/roles/{roleId:guid}", async (Guid tenantId, Guid roleId, RoleRequest request, ClaimsPrincipal user, ICommandHandler<UpdateRoleCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdateRoleCommand(tenantId, user.GetUserId(), roleId, request.Name, request.Description, request.PermissionIds ?? []), cancellationToken))
                .Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        tenant.MapDelete("/roles/{roleId:guid}", async (Guid tenantId, Guid roleId, ClaimsPrincipal user, ICommandHandler<DeleteRoleCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DeleteRoleCommand(tenantId, user.GetUserId(), roleId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        tenant.MapGet("/members/{userId:guid}/roles", async (Guid tenantId, Guid userId, IQueryHandler<GetMemberRolesQuery, IReadOnlyList<MemberRoleResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetMemberRolesQuery(tenantId, userId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageRoles)
            .Produces<IReadOnlyList<MemberRoleResponse>>();

        tenant.MapPut("/members/{userId:guid}/roles", async (Guid tenantId, Guid userId, SetMemberRolesRequest request, ClaimsPrincipal user, ICommandHandler<SetMemberRolesCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SetMemberRolesCommand(tenantId, user.GetUserId(), userId, request.Roles ?? []), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}
