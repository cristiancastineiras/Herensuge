namespace Herensuge.Common.Domain;

/// <summary>
/// Keyed hash (HMAC-SHA-256) of a secret that is stored only to be compared later: recovery codes, one-time
/// codes and links, invitation tokens, device ids. The key lives outside the database, so a copy of the
/// database is not enough to brute-force short codes.
/// </summary>
public interface ISecretHasher
{
    /// <param name="purpose">Separates the uses: the same secret hashes differently in each one.</param>
    /// <param name="secret">Value to hash.</param>
    /// <returns>Lower-case hexadecimal (64 characters).</returns>
    string Hash(string purpose, string secret);
}
