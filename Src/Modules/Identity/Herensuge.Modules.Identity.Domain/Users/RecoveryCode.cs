using System.Security.Cryptography;
using System.Text;

namespace Herensuge.Modules.Identity.Domain.Users;

public sealed class RecoveryCode
{
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

    internal static RecoveryCode Create(Guid userId, string code, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        CodeHash = Hash(code),
        CreatedAtUtc = utcNow,
    };

    /// <summary>Format xxxxx-xxxxx (about 49 bits of entropy).</summary>
    internal static string Generate()
    {
        string raw = RandomNumberGenerator.GetString(Alphabet, 10);
        return $"{raw[..5]}-{raw[5..]}";
    }

    internal static string Hash(string code)
    {
        string normalized = code.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    internal bool Matches(string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(hash));

    internal void MarkUsed(DateTime utcNow) => UsedAtUtc = utcNow;
}
