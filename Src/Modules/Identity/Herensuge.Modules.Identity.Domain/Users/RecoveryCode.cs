using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Users;

/// <summary>Single-use backup code. Only its keyed hash (HMAC with a key outside the database) is stored.</summary>
public sealed class RecoveryCode
{
    private const string HashPurpose = "identity.user_recovery_codes";

    // No ambiguous characters (0/o, 1/l/i).
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    private RecoveryCode()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    internal static RecoveryCode Create(Guid userId, string code, ISecretHasher hasher, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        CodeHash = Hash(code, hasher),
        CreatedAtUtc = utcNow,
    };

    /// <summary>Format xxxxx-xxxxx (about 49 bits of entropy).</summary>
    internal static string Generate()
    {
        string raw = RandomNumberGenerator.GetString(Alphabet, 10);
        return $"{raw[..5]}-{raw[5..]}";
    }

    // About 49 bits: a plain SHA-256 could be brute-forced from a copy of the database; the keyed hash cannot.
    internal static string Hash(string code, ISecretHasher hasher)
    {
        string normalized = code.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
        return hasher.Hash(HashPurpose, normalized);
    }

    internal bool Matches(string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(hash));

    internal void MarkUsed(DateTime utcNow) => UsedAtUtc = utcNow;
}
