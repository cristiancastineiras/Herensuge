using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Presentation.Endpoints;
using Herensuge.Common.Presentation.Results;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Application.Clients.CreateClient;
using Herensuge.Modules.Oidc.Application.Clients.DeleteClient;
using Herensuge.Modules.Oidc.Application.Clients.GetClients;
using Herensuge.Modules.Oidc.Application.Clients.RotateClientSecret;
using Herensuge.Modules.Oidc.Application.Clients.UpdateClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Herensuge.Modules.Oidc.Presentation.Platform;

public sealed record CreateClientRequest(
    string ClientId,
    string DisplayName,
    bool IsConfidential,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes);

public sealed record UpdateClientRequest(
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes);

public sealed record ClientSecretResponse(string ClientSecret);

/// <summary>/api/v1/platform/clients: OIDC clients of the SaaS applications (platform admin + passkey).</summary>
internal sealed class ClientEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/platform/clients")
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin);

        group.MapGet("/", async (IQueryHandler<GetClientsQuery, IReadOnlyList<ClientResponse>> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new GetClientsQuery(), cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<IReadOnlyList<ClientResponse>>();

        group.MapPost("/", async (CreateClientRequest request, ICommandHandler<CreateClientCommand, ClientCreatedResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(
                new CreateClientCommand(
                    request.ClientId,
                    request.DisplayName,
                    request.IsConfidential,
                    request.RedirectUris,
                    request.PostLogoutRedirectUris ?? [],
                    request.Scopes),
                cancellationToken)).Match(value => Results.Ok(value)))
            .Produces<ClientCreatedResponse>();

        group.MapPatch("/{clientId}", async (string clientId, UpdateClientRequest request, ICommandHandler<UpdateClientCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(
                new UpdateClientCommand(clientId, request.DisplayName, request.RedirectUris, request.PostLogoutRedirectUris ?? [], request.Scopes),
                cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{clientId}/secret", async (string clientId, ICommandHandler<RotateClientSecretCommand, string> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RotateClientSecretCommand(clientId), cancellationToken)).Match(secret => Results.Ok(new ClientSecretResponse(secret))))
            .Produces<ClientSecretResponse>();

        group.MapDelete("/{clientId}", async (string clientId, ICommandHandler<DeleteClientCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DeleteClientCommand(clientId), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }
}
