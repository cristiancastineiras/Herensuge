namespace Herensuge.Modules.Identity.Application.Abstractions;

public interface ITotpService
{
    TotpSetup GenerateSecret(string accountName);

    TotpVerification Verify(string protectedSecret, string code);
}

/// <param name="ProtectedSecret">Encrypted secret to store.</param>
/// <param name="SharedKey">Base32 key the user may type in the authenticator app.</param>
/// <param name="AuthenticatorUri">otpauth:// URI rendered as a QR code.</param>
public sealed record TotpSetup(string ProtectedSecret, string SharedKey, string AuthenticatorUri);

public readonly record struct TotpVerification(bool IsValid, long TimeStep);
