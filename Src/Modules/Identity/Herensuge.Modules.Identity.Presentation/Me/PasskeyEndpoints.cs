using System.Security.Claims;
using System.Text.Json;
using Herensuge.Modules.Identity.Application.Passkeys.AddPasskey;
using Herensuge.Modules.Identity.Application.Passkeys.CreatePasskeyRegistrationOptions;
using Herensuge.Modules.Identity.Application.Passkeys.GetPasskeys;
using Herensuge.Modules.Identity.Application.Passkeys.RemovePasskey;
using Herensuge.Modules.Identity.Application.Passkeys.RenamePasskey;

namespace Herensuge.Modules.Identity.Presentation.Me;

public sealed record AddPasskeyRequest(JsonElement Credential, string? Name);

public sealed record RenamePasskeyRequest(string Name);

public sealed record CreatedResult(Guid Id);

internal sealed class PasskeyEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = MeRoutes.Group(app);

        group.MapGet("/passkeys", async (ClaimsPrincipal user, IQueryHandler<GetPasskeysQuery, IReadOnlyList<PasskeyResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetPasskeysQuery(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapPost("/passkeys/options", async (ClaimsPrincipal user, ICommandHandler<CreatePasskeyRegistrationOptionsCommand, string> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreatePasskeyRegistrationOptionsCommand(user.GetUserId()), cancellationToken))
                    .Match(options => Results.Content(options, "application/json")))
            .RequireRecentAuthentication();

        group.MapPost("/passkeys", async (AddPasskeyRequest request, ClaimsPrincipal user, ICommandHandler<AddPasskeyCommand, Guid> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AddPasskeyCommand(user.GetUserId(), request.Credential.GetRawText(), request.Name ?? string.Empty), cancellationToken))
                    .Match(id => Results.Ok(new CreatedResult(id))))
            .RequireRecentAuthentication();

        group.MapPatch("/passkeys/{passkeyId:guid}", async (Guid passkeyId, RenamePasskeyRequest request, ClaimsPrincipal user, ICommandHandler<RenamePasskeyCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RenamePasskeyCommand(user.GetUserId(), passkeyId, request.Name), cancellationToken)).Match(Results.NoContent));

        group.MapDelete("/passkeys/{passkeyId:guid}", async (Guid passkeyId, ClaimsPrincipal user, ICommandHandler<RemovePasskeyCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RemovePasskeyCommand(user.GetUserId(), passkeyId), cancellationToken)).Match(Results.NoContent))
            .RequireRecentAuthentication();
    }
}
