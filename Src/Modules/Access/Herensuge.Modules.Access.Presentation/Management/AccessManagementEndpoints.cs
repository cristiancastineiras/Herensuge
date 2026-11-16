using System.Security.Claims;
using Herensuge.Modules.Access.Application.Assignments;
using Herensuge.Modules.Access.Application.Catalog.GetAccessCatalog;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Application.Groups;
using Herensuge.Modules.Access.Application.Plans;
using Herensuge.Modules.Access.Application.ResourceServers.GetResourceServers;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Application.Roles;
using Herensuge.Modules.Access.Application.ServiceAccounts;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;

namespace Herensuge.Modules.Access.Presentation.Management;

public sealed record RoleRequest(string Name, string? Description, IReadOnlyList<Guid> PermissionIds);

public sealed record RoleCreatedResponse(Guid Id);

public sealed record SetRolesRequest(IReadOnlyList<RoleGrant> Roles);

public sealed record GroupRequest(string Name, string? Description);

public sealed record GroupMembersRequest(IReadOnlyList<Guid> UserIds);

public sealed record CreatedIdResponse(Guid Id);

public sealed record ServiceAccountRequest(string Name);

public sealed record SimulateAccessRequest(Guid UserId, string ResourceServer, string Permission);

public sealed record PlanRequest(string Key, string Name, IReadOnlyList<string> Features);

/// <param name="PlanKey">Null: the default plan.</param>
public sealed record TenantPlanRequest(string? PlanKey);

/// <summary>
/// Platform: applications and their manifests. Organization: what can be granted, roles, groups, service accounts,
/// the roles of each subject, the simulator and the plan (read only). Bearer tokens through the portal BFF.
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

        MapSubjectRoles(tenant, "/members/{subjectId:guid}/roles", SubjectTypes.User, TenantAdministration.ManageRoles);
        MapSubjectRoles(tenant, "/groups/{subjectId:guid}/roles", SubjectTypes.Group, TenantAdministration.ManageRoles);
        MapSubjectRoles(tenant, "/service-accounts/{subjectId:guid}/roles", SubjectTypes.Client, TenantAdministration.ManageServiceAccounts);

        MapGroups(tenant);
        MapServiceAccounts(tenant);

        tenant.MapPost("/access/simulate", async (Guid tenantId, SimulateAccessRequest request, ClaimsPrincipal user, IQueryHandler<SimulateAccessQuery, SimulationResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SimulateAccessQuery(tenantId, user.GetUserId(), request.UserId, request.ResourceServer, request.Permission), cancellationToken))
                .Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageRoles)
            .Produces<SimulationResponse>();

        tenant.MapGet("/plan", async (Guid tenantId, IQueryHandler<GetTenantPlanQuery, TenantPlanDetailsResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetTenantPlanQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<TenantPlanDetailsResponse>();
    }

    /// <summary>GET and PUT of the roles of a member, a group or a service account.</summary>
    private static void MapSubjectRoles(RouteGroupBuilder tenant, string pattern, string subjectType, string permission)
    {
        tenant.MapGet(pattern, async (Guid tenantId, Guid subjectId, IQueryHandler<GetSubjectRolesQuery, IReadOnlyList<AssignedRoleResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetSubjectRolesQuery(tenantId, subjectId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(permission)
            .Produces<IReadOnlyList<AssignedRoleResponse>>();

        tenant.MapPut(pattern, async (Guid tenantId, Guid subjectId, SetRolesRequest request, ClaimsPrincipal user, ICommandHandler<SetRolesCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SetRolesCommand(tenantId, user.GetUserId(), subjectType, subjectId, request.Roles ?? []), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }

    private static void MapGroups(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/groups", async (Guid tenantId, IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetGroupsQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageRoles)
            .Produces<IReadOnlyList<GroupResponse>>();

        tenant.MapPost("/groups", async (Guid tenantId, GroupRequest request, ClaimsPrincipal user, ICommandHandler<CreateGroupCommand, Guid> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new CreateGroupCommand(tenantId, user.GetUserId(), request.Name, request.Description), cancellationToken))
                .Match(id => Results.Ok(new CreatedIdResponse(id))))
            .Produces<CreatedIdResponse>();

        tenant.MapPatch("/groups/{groupId:guid}", async (Guid tenantId, Guid groupId, GroupRequest request, ClaimsPrincipal user, ICommandHandler<UpdateGroupCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdateGroupCommand(tenantId, user.GetUserId(), groupId, request.Name, request.Description), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        tenant.MapDelete("/groups/{groupId:guid}", async (Guid tenantId, Guid groupId, ClaimsPrincipal user, ICommandHandler<DeleteGroupCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DeleteGroupCommand(tenantId, user.GetUserId(), groupId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        tenant.MapPut("/groups/{groupId:guid}/members", async (Guid tenantId, Guid groupId, GroupMembersRequest request, ClaimsPrincipal user, ICommandHandler<SetGroupMembersCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SetGroupMembersCommand(tenantId, user.GetUserId(), groupId, request.UserIds ?? []), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }

    private static void MapServiceAccounts(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/service-accounts", async (Guid tenantId, IQueryHandler<GetServiceAccountsQuery, IReadOnlyList<ServiceAccountResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetServiceAccountsQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequirePermission(TenantAdministration.ManageServiceAccounts)
            .Produces<IReadOnlyList<ServiceAccountResponse>>();

        tenant.MapPost("/service-accounts", async (Guid tenantId, ServiceAccountRequest request, ClaimsPrincipal user, ICommandHandler<CreateServiceAccountCommand, ServiceAccountCreatedResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new CreateServiceAccountCommand(tenantId, user.GetUserId(), request.Name), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<ServiceAccountCreatedResponse>();

        tenant.MapPost("/service-accounts/{serviceAccountId:guid}/secret", async (Guid tenantId, Guid serviceAccountId, ClaimsPrincipal user, ICommandHandler<RotateServiceAccountSecretCommand, ServiceAccountSecretResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RotateServiceAccountSecretCommand(tenantId, user.GetUserId(), serviceAccountId), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<ServiceAccountSecretResponse>();

        tenant.MapDelete("/service-accounts/{serviceAccountId:guid}", async (Guid tenantId, Guid serviceAccountId, ClaimsPrincipal user, ICommandHandler<DeleteServiceAccountCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DeleteServiceAccountCommand(tenantId, user.GetUserId(), serviceAccountId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}

/// <summary>Platform: plans (sets of features) and the plan of each organization (plan D5).</summary>
internal sealed class PlanEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder platform = app.MapGroup("/api/v1/platform")
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin);

        platform.MapGet("/features", async (IQueryHandler<GetFeaturesQuery, IReadOnlyList<FeatureResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetFeaturesQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<IReadOnlyList<FeatureResponse>>();

        platform.MapGet("/plans", async (IQueryHandler<GetPlansQuery, IReadOnlyList<PlanResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetPlansQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<IReadOnlyList<PlanResponse>>();

        platform.MapPost("/plans", async (PlanRequest request, ICommandHandler<CreatePlanCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new CreatePlanCommand(request.Key, request.Name, request.Features ?? []), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        platform.MapPatch("/plans/{key}", async (string key, PlanRequest request, ICommandHandler<UpdatePlanCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdatePlanCommand(key, request.Name, request.Features ?? []), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        platform.MapPost("/plans/{key}/default", async (string key, ICommandHandler<SetDefaultPlanCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SetDefaultPlanCommand(key), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        platform.MapDelete("/plans/{key}", async (string key, ICommandHandler<DeletePlanCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DeletePlanCommand(key), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        platform.MapGet("/tenant-plans", async (IQueryHandler<GetTenantPlansQuery, IReadOnlyList<TenantPlanResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetTenantPlansQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<IReadOnlyList<TenantPlanResponse>>();

        platform.MapPut("/tenants/{tenantId:guid}/plan", async (Guid tenantId, TenantPlanRequest request, ICommandHandler<SetTenantPlanCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new SetTenantPlanCommand(tenantId, request.PlanKey), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}
