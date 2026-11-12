using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Herensuge.Common.Infrastructure.Security;

/// <summary>
/// Keys that protect the data at rest. They never go into the database: back them up apart from it, because
/// without them the encrypted data cannot be read. Generate each one with <c>openssl rand -base64 32</c>.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// AES-256 keys by id (lower-case letters and digits). To rotate, add a new id and make it active: older ids
    /// stay configured so existing data still decrypts.
    /// </summary>
    public Dictionary<string, string> EncryptionKeys { get; set; } = [];

    /// <summary>Id of the key that encrypts new data.</summary>
    public string ActiveEncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// HMAC key for the blind indexes (email lookups), the secret hashes (codes, tokens) and the password pepper.
    /// Changing it invalidates them, so it is not rotated like the encryption keys.
    /// </summary>
    public string HashingKey { get; set; } = string.Empty;
}

/// <summary>Validated key material, derived once at startup.</summary>
public sealed partial class SecurityKeys
{
    private const int KeySize = 32;

    private SecurityKeys(Dictionary<string, byte[]> encryptionKeys, string activeKeyId, byte[] hashingKey)
    {
        EncryptionKeys = encryptionKeys;
        ActiveKeyId = activeKeyId;
        SecretHashKey = Derive(hashingKey, "herensuge/secret-hash/v1");
        PasswordPepper = Derive(hashingKey, "herensuge/password-pepper/v1");
    }

    internal IReadOnlyDictionary<string, byte[]> EncryptionKeys { get; }

    internal string ActiveKeyId { get; }

    internal byte[] SecretHashKey { get; }

    /// <summary>Secret mixed into every password hash (Argon2id "secret" input), so stolen hashes cannot be cracked offline.</summary>
    public byte[] PasswordPepper { get; }

    /// <summary>Fails at startup, with instructions, when a key is missing or malformed.</summary>
    public static SecurityKeys From(SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        const string Help = " Generate keys with 'openssl rand -base64 32' (see Docker/.env.example).";

        if (options.EncryptionKeys.Count == 0)
        {
            throw new InvalidOperationException("Security:EncryptionKeys is empty: personal data cannot be encrypted." + Help);
        }

        var encryptionKeys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string id, string value) in options.EncryptionKeys)
        {
            if (!KeyIdPattern().IsMatch(id))
            {
                throw new InvalidOperationException($"Security:EncryptionKeys:{id}: ids use 1-16 lower-case letters and digits.");
            }

            encryptionKeys[id] = Decode(value, $"Security:EncryptionKeys:{id}", Help);
        }

        if (!encryptionKeys.ContainsKey(options.ActiveEncryptionKey))
        {
            throw new InvalidOperationException(
                $"Security:ActiveEncryptionKey ('{options.ActiveEncryptionKey}') must be one of Security:EncryptionKeys.");
        }

        byte[] hashingKey = Decode(options.HashingKey, "Security:HashingKey", Help);
        if (encryptionKeys.Values.Any(key => CryptographicOperations.FixedTimeEquals(key, hashingKey)))
        {
            throw new InvalidOperationException("Security:HashingKey must be different from every encryption key.");
        }

        return new SecurityKeys(encryptionKeys, options.ActiveEncryptionKey, hashingKey);
    }

    private static byte[] Decode(string? value, string name, string help)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(value ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{name} is not valid base64.{help}");
        }

        return key.Length == KeySize
            ? key
            : throw new InvalidOperationException($"{name} must be {KeySize} random bytes in base64 (it is {key.Length}).{help}");
    }

    // Independent subkeys: one secret, separate uses (HKDF, RFC 5869).
    private static byte[] Derive(byte[] key, string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, KeySize, salt: [], info: Encoding.UTF8.GetBytes(purpose));

    [GeneratedRegex("^[a-z0-9]{1,16}$")]
    private static partial Regex KeyIdPattern();
}
