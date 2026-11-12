using System.Security.Claims;
using System.Text.Json;
using Herensuge.Modules.Identity.Application.Mfa.BeginTotpSetup;
using Herensuge.Modules.Identity.Application.Mfa.ConfirmTotpSetup;
using Herensuge.Modules.Identity.Application.SignIn;
using Herensuge.Modules.Identity.Application.SignIn.CreatePasskeySignInOptions;
using Herensuge.Modules.Identity.Application.SignIn.SendSmsMfaCode;
using Herensuge.Modules.Identity.Application.SignIn.SignInWithPasskey;
using Herensuge.Modules.Identity.Application.SignIn.SignInWithPassword;
using Herensuge.Modules.Identity.Application.SignIn.VerifyMfa;
using Herensuge.Modules.Identity.Application.Users.GetMfaChallenge;

namespace Herensuge.Modules.Identity.Presentation.Auth;

public sealed record SignInRequest(string Email, string Password);

public sealed record VerifyMfaRequest(string Method, string Code);

public sealed record CodeRequest(string Code);

public sealed record RecoveryCodesResult(IReadOnlyList<string> RecoveryCodes);

/// <summary>Password, second factor, passkey and MFA enrolment during sign-in.</summary>
internal sealed class SignInEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = AuthRoutes.Group(app);

        group.MapPost("/sign-in", async (
                SignInRequest request,
                HttpContext httpContext,
                ICommandHandler<SignInWithPasswordCommand, SignInResult> handler,
                CancellationToken cancellationToken) =>
            {
                Result<SignInResult> result = await handler.Handle(new SignInWithPasswordCommand(request.Email, request.Password), cancellationToken);
                return result.IsFailure ? ApiResults.Problem(result) : await SignInCookies.CompleteAsync(httpContext, result.Value);
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<SignInResponse>();

        group.MapGet("/sign-in/mfa", async (
            HttpContext httpContext,
            IQueryHandler<GetMfaChallengeQuery, MfaChallengeResponse> handler,
            CancellationToken cancellationToken) =>
        {
            MfaContext? mfa = await MfaContext.ResolveAsync(httpContext);
            if (mfa is null)
            {
                return AuthProblems.NotSignedIn();
            }

            return (await handler.Handle(new GetMfaChallengeQuery(mfa.UserId), cancellationToken)).Match(value => Results.Ok(value));
        })
            .Produces<MfaChallengeResponse>();

        group.MapPost("/sign-in/mfa", async (
                VerifyMfaRequest request,
                HttpContext httpContext,
                ICommandHandler<VerifyMfaCommand, SignInResult> handler,
                CancellationToken cancellationToken) =>
            {
                MfaContext? mfa = await MfaContext.ResolveAsync(httpContext);
                if (mfa is null)
                {
                    return AuthProblems.NotSignedIn();
                }

                Result<SignInResult> result = await handler.Handle(
                    new VerifyMfaCommand(mfa.UserId, request.Method, request.Code, mfa.FirstFactorMethods, mfa.StepUpSessionId),
                    cancellationToken);

                return result.IsFailure ? ApiResults.Problem(result) : await SignInCookies.CompleteAsync(httpContext, result.Value, mfa.TenantId);
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<SignInResponse>();

        group.MapPost("/sign-in/mfa/sms", async (
                HttpContext httpContext,
                ICommandHandler<SendSmsMfaCodeCommand> handler,
                CancellationToken cancellationToken) =>
            {
                MfaContext? mfa = await MfaContext.ResolveAsync(httpContext);
                if (mfa is null)
                {
                    return AuthProblems.NotSignedIn();
                }

                return (await handler.Handle(new SendSmsMfaCodeCommand(mfa.UserId), cancellationToken)).Match(() => Results.Accepted());
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/passkeys/options", async (
            ICommandHandler<CreatePasskeySignInOptionsCommand, string> handler,
            CancellationToken cancellationToken) =>
            (await handler.Handle(new CreatePasskeySignInOptionsCommand(), cancellationToken))
                .Match(options => Results.Content(options, "application/json")))
            .Produces<JsonElement>();

        group.MapPost("/passkeys/sign-in", async (
                JsonElement assertion,
                HttpContext httpContext,
                ICommandHandler<SignInWithPasskeyCommand, SignInResult> handler,
                CancellationToken cancellationToken) =>
            {
                Result<SignInResult> result = await handler.Handle(new SignInWithPasskeyCommand(assertion.GetRawText()), cancellationToken);
                return result.IsFailure ? ApiResults.Problem(result) : await SignInCookies.CompleteAsync(httpContext, result.Value);
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<SignInResponse>();

        // MFA enrolment required by an organization, done from the login SPA with the SSO cookie.
        group.MapPost("/mfa/totp", async (
            HttpContext httpContext,
            ICommandHandler<BeginTotpSetupCommand, TotpSetupResponse> handler,
            CancellationToken cancellationToken) =>
        {
            ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
            if (principal is null)
            {
                return AuthProblems.NotSignedIn();
            }

            return (await handler.Handle(new BeginTotpSetupCommand(principal.GetUserId()), cancellationToken)).Match(value => Results.Ok(value));
        })
            .Produces<TotpSetupResponse>();

        group.MapPost("/mfa/totp/confirm", async (
                CodeRequest request,
                HttpContext httpContext,
                ICommandHandler<ConfirmTotpSetupCommand, ConfirmTotpSetupResponse> handler,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal? principal = await IdentityProviderSession.AuthenticateAsync(httpContext);
                if (principal is null)
                {
                    return AuthProblems.NotSignedIn();
                }

                Result<ConfirmTotpSetupResponse> result = await handler.Handle(
                    new ConfirmTotpSetupCommand(principal.GetUserId(), request.Code, principal.GetSessionId()),
                    cancellationToken);
                if (result.IsFailure)
                {
                    return ApiResults.Problem(result);
                }

                if (result.Value is { SessionMethods: { } methods, AuthenticatedAtUtc: { } authenticatedAt })
                {
                    await IdentityProviderSession.SignInAsync(
                        httpContext,
                        principal.GetUserId(),
                        principal.GetSessionId()!.Value,
                        methods,
                        authenticatedAt,
                        principal.GetTenantId());
                }

                return Results.Ok(new RecoveryCodesResult(result.Value.RecoveryCodes));
            })
            .RequireRateLimiting(RateLimitPolicies.AuthStrict)
            .Produces<RecoveryCodesResult>();
    }
}
