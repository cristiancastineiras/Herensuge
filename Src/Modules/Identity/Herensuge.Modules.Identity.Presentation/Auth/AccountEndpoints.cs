using System.Security.Claims;
using Herensuge.Modules.Identity.Application;
using Herensuge.Modules.Identity.Application.SignIn.SignOut;
using Herensuge.Modules.Identity.Application.Users.ConfirmEmailChange;
using Herensuge.Modules.Identity.Application.Users.GetCurrentUser;
using Herensuge.Modules.Identity.Application.Users.RegisterUser;
using Herensuge.Modules.Identity.Application.Users.RequestPasswordReset;
using Herensuge.Modules.Identity.Application.Users.ResendEmailVerification;
using Herensuge.Modules.Identity.Application.Users.ResetPassword;
using Herensuge.Modules.Identity.Application.Users.VerifyEmail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Presentation.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record TokenRequest(string Token);

public sealed record EmailRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ProvidersResponse(IReadOnlyList<string> Providers);

public sealed record SessionStateResponse(
    bool SignedIn,
    bool MfaPending,
    Guid? UserId,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> AuthenticationMethods,
    Guid? TenantId);

/// <summary>Registration, email verification, password recovery and session state.</summary>
internal sealed class AccountEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = AuthRoutes.Group(app);

        group.MapGet("/providers", (IOptions<ExternalProvidersOptions> options) =>
            Results.Ok(new ProvidersResponse(options.Value.GetConfiguredProviders())))
            .Produces<ProvidersResponse>();

        group.MapPost("/register", async (RegisterRequest request, ICommandHandler<RegisterUserCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RegisterUserCommand(request.Email, request.Password, request.DisplayName), cancellationToken))
                    .Match(() => Results.Accepted()))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/verify-email", async (TokenRequest request, ICommandHandler<VerifyEmailCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new VerifyEmailCommand(request.Token), cancellationToken)).Match(Results.NoContent))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/verify-email/resend", async (EmailRequest request, ICommandHandler<ResendEmailVerificationCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ResendEmailVerificationCommand(request.Email), cancellationToken)).Match(() => Results.Accepted()))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/password/forgot", async (EmailRequest request, ICommandHandler<RequestPasswordResetCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RequestPasswordResetCommand(request.Email), cancellationToken)).Match(() => Results.Accepted()))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/password/reset", async (ResetPasswordRequest request, ICommandHandler<ResetPasswordCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken)).Match(Results.NoContent))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/email-change/confirm", async (TokenRequest request, ICommandHandler<ConfirmEmailChangeCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfirmEmailChangeCommand(request.Token), cancellationToken)).Match(Results.NoContent))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/session", GetSessionAsync)
            .Produces<SessionStateResponse>();

        group.MapPost("/sign-out", async (HttpContext httpContext, ICommandHandler<SignOutCommand> handler, CancellationToken cancellationToken) =>
        {
            ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
            if (principal?.GetSessionId() is { } sessionId)
            {
                await handler.Handle(new SignOutCommand(sessionId), cancellationToken);
            }

            await IdentityProviderSession.SignOutAsync(httpContext);
            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetSessionAsync(
        HttpContext httpContext,
        IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
        CancellationToken cancellationToken)
    {
        ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
        if (principal is null)
        {
            AuthenticateResult pending = await httpContext.AuthenticateAsync(HerensugeAuthenticationSchemes.MfaPending);
            return Results.Ok(new SessionStateResponse(false, pending.Succeeded, null, null, null, [], null));
        }

        Result<CurrentUserResponse> user = await handler.Handle(new GetCurrentUserQuery(principal.GetUserId()), cancellationToken);
        if (user.IsFailure)
        {
            await IdentityProviderSession.SignOutAsync(httpContext);
            return Results.Ok(new SessionStateResponse(false, false, null, null, null, [], null));
        }

        return Results.Ok(new SessionStateResponse(
            true,
            false,
            user.Value.Id,
            user.Value.Email,
            user.Value.DisplayName,
            principal.GetAuthenticationMethods(),
            principal.GetTenantId()));
    }
}
