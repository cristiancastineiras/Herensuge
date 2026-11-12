namespace Herensuge.Modules.Oidc.Application.Clients.UpdateClient;

internal sealed class UpdateClientCommandHandler(IClientRegistry clients) : ICommandHandler<UpdateClientCommand>
{
    public Task<Result> Handle(UpdateClientCommand command, CancellationToken cancellationToken) =>
        clients.UpdateAsync(
            command.ClientId,
            new ClientSettings(command.DisplayName, command.RedirectUris, command.PostLogoutRedirectUris, command.Scopes),
            cancellationToken);
}
