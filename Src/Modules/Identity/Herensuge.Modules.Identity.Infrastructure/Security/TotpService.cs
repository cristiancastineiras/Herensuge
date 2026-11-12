using System.Security.Cryptography;
using Herensuge.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Herensuge.Modules.Identity.Infrastructure.Security;

public sealed class TotpOptions
{
    public const string SectionName = "Identity:Totp";

    /// <summary>Name shown in the authenticator app.</summary>
    public string Issuer { get; set; } = "Herensuge";
}

/// <summary>RFC 6238 (SHA-1, 6 digits, 30 s, ±1 step). Secrets are encrypted with ASP.NET Data Protection.</summary>
internal sealed class TotpService(IDataProtectionProvider dataProtectionProvider, IOptions<TotpOptions> options) : ITotpService
{
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Herensuge.Identity.Totp.v1");

    public TotpSetup GenerateSecret(string accountName)
    {
        string sharedKey = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        string issuer = Uri.EscapeDataString(options.Value.Issuer);
        string label = $"{issuer}:{Uri.EscapeDataString(accountName)}";
        string uri = $"otpauth://totp/{label}?secret={sharedKey}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        return new TotpSetup(_protector.Protect(sharedKey), sharedKey, uri);
    }

    public TotpVerification Verify(string protectedSecret, string code)
    {
        string sharedKey;
        try
        {
            sharedKey = _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return new TotpVerification(false, 0);
        }

        string normalized = code.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        long timeStep = 0;
        bool isValid = normalized.Length == 6
            && normalized.All(char.IsAsciiDigit)
            && new Totp(Base32Encoding.ToBytes(sharedKey)).VerifyTotp(normalized, out timeStep, Window);

        return new TotpVerification(isValid, isValid ? timeStep : 0);
    }
}
