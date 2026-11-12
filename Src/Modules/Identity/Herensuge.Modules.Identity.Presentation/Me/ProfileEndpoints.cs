using System.Security.Claims;
using Herensuge.Modules.Identity.Application.Users.ChangePassword;
using Herensuge.Modules.Identity.Application.Users.DeleteAccount;
using Herensuge.Modules.Identity.Application.Users.GetCurrentUser;
using Herensuge.Modules.Identity.Application.Users.RequestEmailChange;
using Herensuge.Modules.Identity.Application.Users.UpdateProfile;

namespace Herensuge.Modules.Identity.Presentation.Me;

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangeEmailRequest(string NewEmail);

public sealed record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

internal sealed class ProfileEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = MeRoutes.Group(app);

        group.MapGet("/", async (ClaimsPrincipal user, IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetCurrentUserQuery(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapPatch("/", async (UpdateProfileRequest request, ClaimsPrincipal user, ICommandHandler<UpdateProfileCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdateProfileCommand(user.GetUserId(), request.DisplayName), cancellationToken)).Match(Results.NoContent));

        group.MapDelete("/", async (ClaimsPrincipal user, ICommandHandler<DeleteAccountCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteAccountCommand(user.GetUserId()), cancellationToken)).Match(Results.NoContent))
            .RequireRecentAuthentication();

        group.MapPost("/email", async (ChangeEmailRequest request, ClaimsPrincipal user, ICommandHandler<RequestEmailChangeCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RequestEmailChangeCommand(user.GetUserId(), request.NewEmail), cancellationToken)).Match(() => Results.Accepted()))
            .RequireRecentAuthentication();

        group.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal user, ICommandHandler<ChangePasswordCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(
                    new ChangePasswordCommand(user.GetUserId(), request.CurrentPassword, request.NewPassword, user.GetSessionId()),
                    cancellationToken)).Match(Results.NoContent))
            .RequireRecentAuthentication();
    }
}
