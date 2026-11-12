using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Domain.Tokens.Events;

namespace Herensuge.Modules.Identity.Domain.Tokens;

/// <summary>
/// Single-use secret sent by email (links) or SMS (6-digit codes). Only its SHA-256 hash is stored.
/// </summary>
public sealed class OneTimeToken : AggregateRoot
{
    public const int MaxFailedAttempts = 5;

    private OneTimeToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public OneTimeTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Email or phone number the token was sent to (new email for an email change).</summary>
    public string? Target { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public int FailedAttempts { get; private set; }

    public static (OneTimeToken Token, string Value) Issue(Guid userId, OneTimeTokenPurpose purpose, string? target, DateTime utcNow)
    {
        Guid id = Guid.CreateVersion7();
        string value = purpose.UsesNumericCode()
            ? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture)
            : Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var token = new OneTimeToken
        {
            Id = id,
            UserId = userId,
            Purpose = purpose,
            Target = target,
            TokenHash = ComputeHash(id, purpose, value),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow + purpose.Lifetime(),
        };

        token.Raise(new OneTimeTokenIssuedDomainEvent(id, userId, purpose, target, value, token.ExpiresAtUtc));
        return (token, value);
    }

    /// <summary>Hash used to look a link token up. Links are high-entropy, so they need no salt.</summary>
    public static string ComputeLinkHash(string value) => Hash(value.Trim());

    public bool IsActive(DateTime utcNow) =>
        ConsumedAtUtc is null && utcNow < ExpiresAtUtc && FailedAttempts < MaxFailedAttempts;

    public Result Consume(string value, DateTime utcNow)
    {
        if (!IsActive(utcNow))
        {
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        string expected = ComputeHash(Id, Purpose, value.Trim());
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(TokenHash)))
        {
            FailedAttempts++;
            return OneTimeTokenErrors.InvalidOrExpired;
        }

        ConsumedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Superseded by a newer token of the same purpose.</summary>
    public void Invalidate(DateTime utcNow)
    {
        ConsumedAtUtc ??= utcNow;
    }

    // 6-digit codes have little entropy: they are salted with the token id.
    private static string ComputeHash(Guid id, OneTimeTokenPurpose purpose, string value) =>
        Hash(purpose.UsesNumericCode() ? $"{id:N}:{value}" : value);

    private static string Hash(string input) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}

public enum OneTimeTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2,
    EmailChange = 3,
    PhoneVerification = 4,
    SmsMfa = 5,
}

public static class OneTimeTokenPurposeExtensions
{
    public static TimeSpan Lifetime(this OneTimeTokenPurpose purpose) => purpose switch
    {
        OneTimeTokenPurpose.EmailVerification => TimeSpan.FromHours(24),
        OneTimeTokenPurpose.PasswordReset => TimeSpan.FromMinutes(15),
        OneTimeTokenPurpose.EmailChange => TimeSpan.FromHours(1),
        OneTimeTokenPurpose.PhoneVerification => TimeSpan.FromMinutes(10),
        OneTimeTokenPurpose.SmsMfa => TimeSpan.FromMinutes(5),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };

    public static bool UsesNumericCode(this OneTimeTokenPurpose purpose) =>
        purpose is OneTimeTokenPurpose.PhoneVerification or OneTimeTokenPurpose.SmsMfa;
}

public static class OneTimeTokenErrors
{
    public static readonly Error InvalidOrExpired = Error.Validation(
        "Tokens.InvalidOrExpired",
        "The link or code is not valid or has expired.");
}
