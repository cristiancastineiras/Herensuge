namespace Herensuge.Modules.Oidc.Application.Clients.RotateClientSecret;

internal sealed class RotateClientSecretCommandHandler(IClientRegistry clients) : ICommandHandler<RotateClientSecretCommand, string>
{
    public Task<Result<string>> Handle(RotateClientSecretCommand command, CancellationToken cancellationToken) =>
        clients.RotateSecretAsync(command.ClientId, cancellationToken);
}
