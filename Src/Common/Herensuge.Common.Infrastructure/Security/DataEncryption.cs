using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;

namespace Herensuge.Common.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a random 96-bit nonce per value. Format: <c>enc:v1:{keyId}:{base64url(nonce|ciphertext|tag)}</c>.
/// The key id and the purpose (column) are authenticated as associated data: a value moved to another column, or
/// altered in any way, fails to decrypt instead of returning wrong data.
/// </summary>
public sealed class AesGcmDataEncryptor(SecurityKeys keys) : IDataEncryptor
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string Encrypt(string plaintext, string purpose)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        string keyId = keys.ActiveKeyId;
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] payload = new byte[NonceSize + data.Length + TagSize];
        Span<byte> nonce = payload.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(keys.EncryptionKeys[keyId], TagSize);
        aes.Encrypt(nonce, data, payload.AsSpan(NonceSize, data.Length), payload.AsSpan(NonceSize + data.Length), AssociatedData(keyId, purpose));

        return string.Concat(Prefix, keyId, ":", Base64Url.EncodeToString(payload));
    }

    public string Decrypt(string ciphertext, string purpose)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        int separator = ciphertext.IndexOf(':', Prefix.Length);
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal) || separator < 0)
        {
            throw new CryptographicException($"The value of '{purpose}' is not encrypted data.");
        }

        string keyId = ciphertext[Prefix.Length..separator];
        if (!keys.EncryptionKeys.TryGetValue(keyId, out byte[]? key))
        {
            throw new CryptographicException($"Encryption key '{keyId}' is not configured (Security:EncryptionKeys).");
        }

        byte[] payload = Base64Url.DecodeFromChars(ciphertext.AsSpan(separator + 1));
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException($"The value of '{purpose}' is truncated.");
        }

        int length = payload.Length - NonceSize - TagSize;
        byte[] data = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            payload.AsSpan(0, NonceSize),
            payload.AsSpan(NonceSize, length),
            payload.AsSpan(NonceSize + length),
            data,
            AssociatedData(keyId, purpose));

        return Encoding.UTF8.GetString(data);
    }

    private static byte[] AssociatedData(string keyId, string purpose) => Encoding.UTF8.GetBytes($"{keyId}|{purpose}");
}

/// <summary>HMAC-SHA-256 with a subkey of Security:HashingKey. The purpose is part of the message.</summary>
public sealed class HmacSecretHasher(SecurityKeys keys) : ISecretHasher
{
    public string Hash(string purpose, string secret)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(secret);

        // Length-prefixed purpose: ("a", "b|c") and ("a|b", "c") never produce the same message.
        byte[] message = Encoding.UTF8.GetBytes($"{purpose.Length}:{purpose}|{secret}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(keys.SecretHashKey, message));
    }
}
