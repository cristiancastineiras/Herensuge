using System.Diagnostics.CodeAnalysis;

namespace Herensuge.Common.Application.Security;

/// <summary>
/// Authenticated encryption (AES-256-GCM) of personal data at rest: emails, names, phone numbers, IP addresses,
/// user agents, audit metadata and event payloads. The keys live outside the database (configuration), so a
/// copy of the database or a backup reveals nothing.
/// </summary>
public interface IDataEncryptor
{
    /// <param name="plaintext">Value to protect.</param>
    /// <param name="purpose">Where the value lives (for example "identity.users.email"). It is authenticated, so a
    /// ciphertext copied into another column does not decrypt.</param>
    string Encrypt(string plaintext, string purpose);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The value was tampered with, belongs to
    /// another column or its key is not configured.</exception>
    string Decrypt(string ciphertext, string purpose);
}

public static class DataEncryptorExtensions
{
    [return: NotNullIfNotNull(nameof(plaintext))]
    public static string? EncryptNullable(this IDataEncryptor encryptor, string? plaintext, string purpose) =>
        plaintext is null ? null : encryptor.Encrypt(plaintext, purpose);

    [return: NotNullIfNotNull(nameof(ciphertext))]
    public static string? DecryptNullable(this IDataEncryptor encryptor, string? ciphertext, string purpose) =>
        ciphertext is null ? null : encryptor.Decrypt(ciphertext, purpose);
}
