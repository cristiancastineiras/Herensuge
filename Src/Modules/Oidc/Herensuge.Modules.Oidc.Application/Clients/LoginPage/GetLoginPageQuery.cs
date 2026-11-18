namespace Herensuge.Modules.Oidc.Application.Clients.LoginPage;

/// <summary>Login page of a client, for the login SPA (anonymous) and for the platform editor.</summary>
public sealed record GetLoginPageQuery(string ClientId) : IQuery<ClientLoginPage>;

internal sealed class GetLoginPageQueryHandler(IClientRegistry clients) : IQueryHandler<GetLoginPageQuery, ClientLoginPage>
{
    public async Task<Result<ClientLoginPage>> Handle(GetLoginPageQuery query, CancellationToken cancellationToken) =>
        await clients.GetLoginPageAsync(query.ClientId, cancellationToken) is { } page ? page : OidcErrors.ClientNotFound;
}
