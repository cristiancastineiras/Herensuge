using System.Security.Claims;
using System.Text.Json;
using Herensuge.Modules.Identity.Application.Support;
using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Presentation.Auth;

namespace Herensuge.Modules.Identity.Presentation.Support;

public sealed record SupportSessionRequest(string Email, string Reason);

/// <param name="Url">https://&lt;idp&gt;/support#&lt;token&gt;: open it in a private window and confirm with your passkey.</param>
public sealed record SupportSessionResponse(string Url, DateTime ExpiresAtUtc);

public sealed record RedeemSupportRequest(string Token, JsonElement Assertion);

/// <summary>Read-only support sessions (plan D11): the platform asks for one, the login SPA redeems it.</summary>
internal sealed class SupportEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/platform/support-sessions", async (
                SupportSessionRequest request,
                ClaimsPrincipal user,
                HttpContext httpContext,
                ICommandHandler<StartSupportSessionCommand, SupportTicketResponse> handler,
                CancellationToken cancellationToken) =>
            {
                Result<SupportTicketResponse> ticket = await handler.Handle(
                    new StartSupportSessionCommand(user.GetUserId(), request.Email ?? string.Empty, request.Reason ?? string.Empty),
                    cancellationToken);

                // The token travels in the fragment: browsers never send it, so it never reaches a log.
                HttpRequest http = httpContext.Request;
                return ticket.Match(value => Results.Ok(new SupportSessionResponse(
                    $"{http.Scheme}://{http.Host}{http.PathBase}/support#{value.Token}",
                    value.ExpiresAtUtc)));
            })
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin)
            .Produces<SupportSessionResponse>();

        AuthRoutes.Group(app).MapPost("/support/redeem", async (
                RedeemSupportRequest request,
                HttpContext httpContext,
                ICommandHandler<RedeemSupportTicketCommand, SupportSignInResult> handler,
                CancellationToken cancellationToken) =>
            {
                // A browser with a Herensuge session would mix it with the user's: private window only, link untouched.
                if (await IdentityProviderSession.AuthenticateAsync(httpContext) is not null)
                {
                    return ApiResults.Problem(SupportErrors.AlreadySignedIn);
                }

                Result<SupportSignInResult> result = await handler.Handle(
                    new RedeemSupportTicketCommand(request.Token ?? string.Empty, request.Assertion.GetRawText()),
                    cancellationToken);
                if (result.IsFailure)
                {
                    return ApiResults.Problem(result);
                }

                SupportSignInResult support = result.Value;
                await IdentityProviderSession.SignInAsync(
                    httpContext,
                    support.SignIn.UserId,
                    support.SignIn.SessionId!.Value,
                    support.SignIn.Methods,
                    support.SignIn.AuthenticatedAtUtc!.Value,
                    tenantId: null,
                    actor: support.ImpersonatorUserId.ToString());
                return Results.Ok(new SignInResponse("signed-in", []));
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<SignInResponse>();
    }
}
