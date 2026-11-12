using System.Security.Claims;
using Herensuge.Modules.Identity.Application.Mfa.BeginTotpSetup;
using Herensuge.Modules.Identity.Application.Mfa.ConfirmPhoneVerification;
using Herensuge.Modules.Identity.Application.Mfa.ConfirmTotpSetup;
using Herensuge.Modules.Identity.Application.Mfa.DisableTotp;
using Herensuge.Modules.Identity.Application.Mfa.RegenerateRecoveryCodes;
using Herensuge.Modules.Identity.Application.Mfa.RemovePhoneNumber;
using Herensuge.Modules.Identity.Application.Mfa.StartPhoneVerification;
using Herensuge.Modules.Identity.Presentation.Auth;

namespace Herensuge.Modules.Identity.Presentation.Me;

public sealed record PhoneNumberRequest(string PhoneNumber);

/// <summary>Authenticator app, phone (SMS) and recovery codes. Every change requires a recent sign-in.</summary>
internal sealed class MfaEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = MeRoutes.Group(app).RequireRecentAuthentication();

        group.MapPost("/mfa/totp", async (ClaimsPrincipal user, ICommandHandler<BeginTotpSetupCommand, TotpSetupResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new BeginTotpSetupCommand(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapPost("/mfa/totp/confirm", async (CodeRequest request, ClaimsPrincipal user, ICommandHandler<ConfirmTotpSetupCommand, ConfirmTotpSetupResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new ConfirmTotpSetupCommand(user.GetUserId(), request.Code, null), cancellationToken))
                .Match(value => Results.Ok(new RecoveryCodesResult(value.RecoveryCodes))));

        group.MapDelete("/mfa/totp", async (ClaimsPrincipal user, ICommandHandler<DisableTotpCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new DisableTotpCommand(user.GetUserId()), cancellationToken)).Match(Results.NoContent));

        group.MapPost("/mfa/recovery-codes", async (ClaimsPrincipal user, ICommandHandler<RegenerateRecoveryCodesCommand, RecoveryCodesResponse> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RegenerateRecoveryCodesCommand(user.GetUserId()), cancellationToken)).Match(value => Results.Ok(value)));

        group.MapPost("/phone", async (PhoneNumberRequest request, ClaimsPrincipal user, ICommandHandler<StartPhoneVerificationCommand> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new StartPhoneVerificationCommand(user.GetUserId(), request.PhoneNumber), cancellationToken)).Match(() => Results.Accepted()))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict);

        group.MapPost("/phone/verify", async (CodeRequest request, ClaimsPrincipal user, ICommandHandler<ConfirmPhoneVerificationCommand, RecoveryCodesResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfirmPhoneVerificationCommand(user.GetUserId(), request.Code), cancellationToken)).Match(value => Results.Ok(value)))
            .RequireRateLimiting(RateLimitPolicies.AuthStrict);

        group.MapDelete("/phone", async (ClaimsPrincipal user, ICommandHandler<RemovePhoneNumberCommand> handler, CancellationToken cancellationToken) =>
            (await handler.Handle(new RemovePhoneNumberCommand(user.GetUserId()), cancellationToken)).Match(Results.NoContent));
    }
}
