using System.Security.Claims;
using Herensuge.Modules.Identity.Application;
using Herensuge.Modules.Identity.Application.SignIn.SignOut;
using Herensuge.Modules.Identity.Application.Users.ConfirmEmailChange;
using Herensuge.Modules.Identity.Application.Users.GetCurrentUser;
using Herensuge.Modules.Identity.Application.Users.ProfileFields;
using Herensuge.Modules.Identity.Application.Users.RegisterUser;
using Herensuge.Modules.Identity.Application.Users.RequestPasswordReset;
using Herensuge.Modules.Identity.Application.Users.ResendEmailVerification;
using Herensuge.Modules.Identity.Application.Users.ResetPassword;
using Herensuge.Modules.Identity.Application.Users.VerifyEmail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Presentation.Auth;

/// <param name="ClientId">Application whose login page the form belongs to (client_id of the returnUrl), if any.</param>
/// <param name="ProfileFields">Values of that application's extra registration fields.</param>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? ClientId,
    IReadOnlyDictionary<string, string?>? ProfileFields);

public sealed record UpdateProfileFieldsRequest(string ClientId, IReadOnlyDictionary<string, string?> Values);

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
    Guid? TenantId,
    IReadOnlyDictionary<string, string> ProfileFields);

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
                (await handler.Handle(
                    new RegisterUserCommand(request.Email, request.Password, request.DisplayName, request.ClientId, request.ProfileFields),
                    cancellationToken)).Match(() => Results.Accepted()))
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

        // /complete-profile: the data an application asks for, before entering it (see /connect/authorize).
        group.MapPut("/profile-fields", async (
                UpdateProfileFieldsRequest request,
                HttpContext httpContext,
                ICommandHandler<UpdateProfileFieldsCommand> handler,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
                if (principal is null)
                {
                    return AuthProblems.NotSignedIn();
                }

                return (await handler.Handle(new UpdateProfileFieldsCommand(principal.GetUserId(), request.ClientId, request.Values), cancellationToken))
                    .Match(Results.NoContent);
            })
            .RejectDuringSupport()
            .Produces(StatusCodes.Status204NoContent);

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
            return Results.Ok(new SessionStateResponse(false, pending.Succeeded, null, null, null, [], null, NoProfileFields));
        }

        Result<CurrentUserResponse> user = await handler.Handle(new GetCurrentUserQuery(principal.GetUserId()), cancellationToken);
        if (user.IsFailure)
        {
            await IdentityProviderSession.SignOutAsync(httpContext);
            return Results.Ok(new SessionStateResponse(false, false, null, null, null, [], null, NoProfileFields));
        }

        return Results.Ok(new SessionStateResponse(
            true,
            false,
            user.Value.Id,
            user.Value.Email,
            user.Value.DisplayName,
            principal.GetAuthenticationMethods(),
            principal.GetTenantId(),
            user.Value.ProfileFields));
    }

    private static readonly Dictionary<string, string> NoProfileFields = [];
}
