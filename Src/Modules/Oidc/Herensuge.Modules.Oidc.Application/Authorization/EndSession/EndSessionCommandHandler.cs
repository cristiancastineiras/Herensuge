using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Oidc.Application.Authorization.EndSession;

internal sealed class EndSessionCommandHandler(IIdentityModuleApi identity) : ICommandHandler<EndSessionCommand>
{
    public async Task<Result> Handle(EndSessionCommand command, CancellationToken cancellationToken)
    {
        await identity.RevokeSessionAsync(command.SessionId, cancellationToken);
        return Result.Success();
    }
}
