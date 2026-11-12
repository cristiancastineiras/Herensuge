namespace Herensuge.Modules.Oidc.Application.Clients.CreateClient;

internal sealed class CreateClientCommandHandler(IClientRegistry clients) : ICommandHandler<CreateClientCommand, ClientCreatedResponse>
{
    public async Task<Result<ClientCreatedResponse>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var definition = new ClientDefinition(
            command.ClientId,
            command.IsConfidential,
            new ClientSettings(command.DisplayName, command.RedirectUris, command.PostLogoutRedirectUris, command.Scopes));

        Result<string?> created = await clients.CreateAsync(definition, cancellationToken);
        return created.IsFailure ? created.Error : new ClientCreatedResponse(command.ClientId, created.Value);
    }
}
