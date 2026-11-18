using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Presentation.Endpoints;
using Herensuge.Common.Presentation.Filters;
using Herensuge.Common.Presentation.Results;
using Herensuge.Common.Presentation.Security;
using Herensuge.Modules.Oidc.Application.Abstractions;
using Herensuge.Modules.Oidc.Application.Clients.LoginPage;
using Herensuge.Modules.Oidc.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Herensuge.Modules.Oidc.Presentation.Platform;

/// <summary>
/// Login page of each client (branding, sign-in methods, registration fields):
/// <list type="bullet">
/// <item>GET /api/v1/auth/clients/{clientId}/login-page: for the login SPA, anonymous (it is shown before signing in);</item>
/// <item>GET and PUT /api/v1/platform/clients/{clientId}/login-page: the platform editor.</item>
/// </list>
/// </summary>
internal sealed class LoginPageEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/clients/{clientId}/login-page", GetAsync)
            .WithTags("Auth")
            .RequireCsrfHeader()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<ClientLoginPage>();

        RouteGroupBuilder platform = app.MapGroup("/api/v1/platform/clients/{clientId}/login-page")
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin);

        platform.MapGet("/", GetAsync).Produces<ClientLoginPage>();

        platform.MapPut("/", async (string clientId, LoginSettings settings, ICommandHandler<UpdateLoginSettingsCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new UpdateLoginSettingsCommand(clientId, settings), cancellationToken)).Match(Results.NoContent))
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetAsync(string clientId, IQueryHandler<GetLoginPageQuery, ClientLoginPage> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetLoginPageQuery(clientId), cancellationToken)).Match(value => Results.Ok(value));
}
