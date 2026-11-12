using System.Security.Claims;
using Herensuge.Modules.Tenancy.Application.Invitations.CreateInvitation;
using Herensuge.Modules.Tenancy.Application.Invitations.GetInvitations;
using Herensuge.Modules.Tenancy.Application.Invitations.RevokeInvitation;
using Herensuge.Modules.Tenancy.Application.Members.ChangeMemberRole;
using Herensuge.Modules.Tenancy.Application.Members.GetMembers;
using Herensuge.Modules.Tenancy.Application.Members.RemoveMember;
using Herensuge.Modules.Tenancy.Application.Tenants.GetTenant;
using Herensuge.Modules.Tenancy.Application.Tenants.GetUserTenants;
using Herensuge.Modules.Tenancy.Application.Tenants.UpdateTenant;

namespace Herensuge.Modules.Tenancy.Presentation.Tenants;

public sealed record UpdateTenantRequest(string Name, bool RequireMfa);

public sealed record ChangeRoleRequest(string Role);

public sealed record CreateInvitationRequest(string Email, string Role);

public sealed record CreatedResponse(Guid Id);

/// <summary>/api/v1/tenants/{tenantId}: organization administration from the portal (Bearer).</summary>
internal sealed class TenantEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/tenants", async (
                ClaimsPrincipal user,
                IQueryHandler<GetUserTenantsQuery, IReadOnlyList<UserTenantResponse>> handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetUserTenantsQuery(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)))
            .WithTags("Me")
            .RequireAuthorization(HerensugePolicies.Api)
            .Produces<IReadOnlyList<UserTenantResponse>>();

        RouteGroupBuilder group = app.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Organization")
            .RequireAuthorization(HerensugePolicies.Api)
            .RequireActiveTenant();

        group.MapGet("/", async (Guid tenantId, IQueryHandler<GetTenantQuery, TenantResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetTenantQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<TenantResponse>();

        group.MapPatch("/", async (Guid tenantId, UpdateTenantRequest request, ClaimsPrincipal user, ICommandHandler<UpdateTenantCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdateTenantCommand(tenantId, user.GetUserId(), request.Name, request.RequireMfa), cancellationToken))
                .Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/members", async (Guid tenantId, IQueryHandler<GetMembersQuery, IReadOnlyList<MemberResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetMembersQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequireTenantAdministrator()
            .Produces<IReadOnlyList<MemberResponse>>();

        group.MapPatch("/members/{userId:guid}", async (Guid tenantId, Guid userId, ChangeRoleRequest request, ClaimsPrincipal user, ICommandHandler<ChangeMemberRoleCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new ChangeMemberRoleCommand(tenantId, user.GetUserId(), userId, request.Role), cancellationToken))
                .Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        // Any member may remove themselves (leave); removing others is checked by the handler.
        group.MapDelete("/members/{userId:guid}", async (Guid tenantId, Guid userId, ClaimsPrincipal user, ICommandHandler<RemoveMemberCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RemoveMemberCommand(tenantId, user.GetUserId(), userId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/invitations", async (Guid tenantId, IQueryHandler<GetInvitationsQuery, IReadOnlyList<InvitationResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetInvitationsQuery(tenantId), cancellationToken)).Match(value => Results.Ok(value)))
            .RequireTenantAdministrator()
            .Produces<IReadOnlyList<InvitationResponse>>();

        group.MapPost("/invitations", async (Guid tenantId, CreateInvitationRequest request, ClaimsPrincipal user, ICommandHandler<CreateInvitationCommand, Guid> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new CreateInvitationCommand(tenantId, user.GetUserId(), request.Email, request.Role), cancellationToken))
                .Match(id => Results.Ok(new CreatedResponse(id))))
            .Produces<CreatedResponse>();

        group.MapDelete("/invitations/{invitationId:guid}", async (Guid tenantId, Guid invitationId, ClaimsPrincipal user, ICommandHandler<RevokeInvitationCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RevokeInvitationCommand(tenantId, user.GetUserId(), invitationId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}
