namespace Herensuge.Modules.Oidc.Application.Clients.GetClients;

internal sealed class GetClientsQueryHandler(IClientRegistry clients) : IQueryHandler<GetClientsQuery, IReadOnlyList<ClientResponse>>
{
    public async Task<Result<IReadOnlyList<ClientResponse>>> Handle(GetClientsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await clients.ListAsync(cancellationToken));
}
