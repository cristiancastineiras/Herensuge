using System.Security.Claims;
using Herensuge.Modules.Identity.Application.ExternalLogins.GetExternalLogins;
using Herensuge.Modules.Identity.Application.ExternalLogins.UnlinkExternalLogin;
using Herensuge.Modules.Identity.Application.Sessions.GetSessions;
using Herensuge.Modules.Identity.Application.Sessions.RevokeOtherSessions;
using Herensuge.Modules.Identity.Application.Sessions.RevokeSession;

namespace Herensuge.Modules.Identity.Presentation.Me;

/// <summary>Open sessions and linked Google/Microsoft accounts.</summary>
internal sealed class SessionEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = MeRoutes.Group(app);

        group.MapGet("/sessions", async (ClaimsPrincipal user, IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetSessionsQuery(user.GetUserId(), user.GetSessionId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, ClaimsPrincipal user, ICommandHandler<RevokeSessionCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RevokeSessionCommand(user.GetUserId(), sessionId), cancellationToken)).Match(Results.NoContent));

        group.MapDelete("/sessions", async (ClaimsPrincipal user, ICommandHandler<RevokeOtherSessionsCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RevokeOtherSessionsCommand(user.GetUserId(), user.GetSessionId()), cancellationToken)).Match(Results.NoContent));

        group.MapGet("/external-logins", async (ClaimsPrincipal user, IQueryHandler<GetExternalLoginsQuery, IReadOnlyList<ExternalLoginResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetExternalLoginsQuery(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapDelete("/external-logins/{provider}", async (string provider, ClaimsPrincipal user, ICommandHandler<UnlinkExternalLoginCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UnlinkExternalLoginCommand(user.GetUserId(), provider), cancellationToken)).Match(Results.NoContent))
            .RequireRecentAuthentication();
    }
}
