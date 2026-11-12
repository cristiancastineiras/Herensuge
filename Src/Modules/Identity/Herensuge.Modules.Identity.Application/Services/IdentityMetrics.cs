using System.Diagnostics.Metrics;
using Herensuge.Modules.Identity.Application.SignIn;

namespace Herensuge.Modules.Identity.Application.Services;

/// <summary>Sign-in and MFA counters (exported by OpenTelemetry through the "Herensuge.*" meters).</summary>
internal static class IdentityMetrics
{
    private static readonly Meter Meter = new("Herensuge.Identity");

    private static readonly Counter<long> SignInAttempts = Meter.CreateCounter<long>(
        "herensuge.sign_in.attempts",
        description: "Sign-in attempts by method (pwd, pop, fed) and result (success, mfa_required or the error code).");

    private static readonly Counter<long> MfaChallenges = Meter.CreateCounter<long>(
        "herensuge.mfa.challenges",
        description: "Second-factor verifications by method (totp, sms, recovery_code) and result.");

    public static Result<SignInResult> TrackSignIn(string method, Result<SignInResult> result)
    {
        SignInAttempts.Add(1, new KeyValuePair<string, object?>("method", method), new KeyValuePair<string, object?>("result", Outcome(result)));
        return result;
    }

    public static Result<SignInResult> TrackMfa(string method, Result<SignInResult> result)
    {
        MfaChallenges.Add(1, new KeyValuePair<string, object?>("method", method), new KeyValuePair<string, object?>("result", Outcome(result)));
        return result;
    }

    private static string Outcome(Result<SignInResult> result) => result.IsFailure
        ? result.Error.Code
        : result.Value.Status switch
        {
            SignInStatus.MfaRequired => "mfa_required",
            SignInStatus.Linked => "linked",
            _ => "success",
        };
}
