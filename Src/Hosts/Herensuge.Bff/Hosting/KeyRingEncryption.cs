using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Herensuge.Bff.Hosting;

/// <summary>
/// AES-256-GCM key (DataProtection:KeyRingKey) that encrypts the Data Protection key ring kept in Valkey. Without
/// it, anyone able to read Valkey could unprotect the session cookies and the tokens of every session.
/// </summary>
public sealed class KeyRingKey
{
    private KeyRingKey(byte[] value) => Value = value;

    internal byte[] Value { get; }

    public static KeyRingKey From(string? base64)
    {
        const string Help = "DataProtection:KeyRingKey must be 32 random bytes in base64 (openssl rand -base64 32).";
        try
        {
            byte[] value = Convert.FromBase64String(base64 ?? string.Empty);
            return value.Length == 32 ? new KeyRingKey(value) : throw new InvalidOperationException(Help);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(Help);
        }
    }
}

public sealed class KeyRingXmlEncryptor(KeyRingKey key) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        string ciphertext = KeyRingCipher.Encrypt(key.Value, plaintextElement.ToString(SaveOptions.DisableFormatting));
        return new EncryptedXmlInfo(new XElement("encryptedKey", ciphertext), typeof(KeyRingXmlDecryptor));
    }
}

/// <summary>Created by Data Protection from the type name stored in the key XML.</summary>
public sealed class KeyRingXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        return XElement.Parse(KeyRingCipher.Decrypt(services.GetRequiredService<KeyRingKey>().Value, encryptedElement.Value));
    }
}

/// <summary>base64url(nonce | ciphertext | tag), with the purpose authenticated as associated data.</summary>
internal static class KeyRingCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("herensuge-bff.data-protection-keys");

    public static string Encrypt(byte[] key, string plaintext)
    {
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] payload = new byte[NonceSize + data.Length + TagSize];
        RandomNumberGenerator.Fill(payload.AsSpan(0, NonceSize));
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(payload.AsSpan(0, NonceSize), data, payload.AsSpan(NonceSize, data.Length), payload.AsSpan(NonceSize + data.Length), AssociatedData);
        return Base64Url.EncodeToString(payload);
    }

    public static string Decrypt(byte[] key, string ciphertext)
    {
        byte[] payload = Base64Url.DecodeFromChars(ciphertext.Trim());
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The Data Protection key is truncated.");
        }

        int length = payload.Length - NonceSize - TagSize;
        byte[] data = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize, length), payload.AsSpan(NonceSize + length), data, AssociatedData);
        return Encoding.UTF8.GetString(data);
    }
}
