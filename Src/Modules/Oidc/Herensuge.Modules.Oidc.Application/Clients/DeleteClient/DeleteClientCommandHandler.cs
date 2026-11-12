namespace Herensuge.Modules.Oidc.Application.Clients.DeleteClient;

internal sealed class DeleteClientCommandHandler(IClientRegistry clients) : ICommandHandler<DeleteClientCommand>
{
    public Task<Result> Handle(DeleteClientCommand command, CancellationToken cancellationToken) =>
        clients.DeleteAsync(command.ClientId, cancellationToken);
}
