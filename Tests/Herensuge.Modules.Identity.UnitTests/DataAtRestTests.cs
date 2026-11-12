using System.Security.Cryptography;
using Herensuge.Common.Infrastructure.Security;

namespace Herensuge.Modules.Identity.UnitTests;

/// <summary>Encryption of personal data, keyed hashes and key validation.</summary>
public sealed class DataAtRestTests
{
    private const string Purpose = "identity.users.email";
    private readonly AesGcmDataEncryptor _encryptor = new(TestKeys.Keys);

    [Fact]
    public void Encryption_round_trips_and_hides_the_value()
    {
        string ciphertext = _encryptor.Encrypt("ana@example.com", Purpose);

        Assert.StartsWith("enc:v1:k1:", ciphertext, StringComparison.Ordinal);
        Assert.DoesNotContain("ana", ciphertext, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ana@example.com", _encryptor.Decrypt(ciphertext, Purpose));
    }

    [Fact]
    public void Every_encryption_uses_a_new_nonce() =>
        Assert.NotEqual(_encryptor.Encrypt("ana@example.com", Purpose), _encryptor.Encrypt("ana@example.com", Purpose));

    [Fact]
    public void A_value_moved_to_another_column_does_not_decrypt()
    {
        string ciphertext = _encryptor.Encrypt("ana@example.com", Purpose);

        Assert.ThrowsAny<CryptographicException>(() => _encryptor.Decrypt(ciphertext, "identity.users.display_name"));
    }

    [Fact]
    public void Tampered_or_plain_values_do_not_decrypt()
    {
        string ciphertext = _encryptor.Encrypt("ana@example.com", Purpose);
        char last = ciphertext[^1];
        string tampered = ciphertext[..^1] + (last == 'A' ? 'B' : 'A');

        Assert.ThrowsAny<CryptographicException>(() => _encryptor.Decrypt(tampered, Purpose));
        Assert.ThrowsAny<CryptographicException>(() => _encryptor.Decrypt("ana@example.com", Purpose));
    }

    [Fact]
    public void After_a_rotation_old_data_still_decrypts_and_new_data_uses_the_new_key()
    {
        string old = _encryptor.Encrypt("ana@example.com", Purpose);
        var rotated = new AesGcmDataEncryptor(TestKeys.Create(("k1", TestKeys.KeyA), ("k2", TestKeys.KeyB)));

        Assert.Equal("ana@example.com", rotated.Decrypt(old, Purpose));
        Assert.StartsWith("enc:v1:k2:", rotated.Encrypt("ana@example.com", Purpose), StringComparison.Ordinal);
    }

    [Fact]
    public void Data_of_a_removed_key_does_not_decrypt()
    {
        string old = _encryptor.Encrypt("ana@example.com", Purpose);
        var other = new AesGcmDataEncryptor(TestKeys.Create(("k2", TestKeys.KeyB)));

        Assert.ThrowsAny<CryptographicException>(() => other.Decrypt(old, Purpose));
    }

    [Fact]
    public void Secret_hashes_are_keyed_and_separated_by_purpose()
    {
        var otherKey = new HmacSecretHasher(TestKeys.Create(TestKeys.KeyB, ("k1", TestKeys.KeyA)));

        Assert.Equal(TestKeys.Hasher.Hash("a", "123456"), TestKeys.Hasher.Hash("a", "123456"));
        Assert.NotEqual(TestKeys.Hasher.Hash("a", "123456"), TestKeys.Hasher.Hash("b", "123456"));
        Assert.NotEqual(TestKeys.Hasher.Hash("a", "123456"), otherKey.Hash("a", "123456"));
        Assert.Matches("^[0-9a-f]{64}$", TestKeys.Hasher.Hash("a", "123456"));
    }

    [Fact]
    public void Missing_or_weak_keys_stop_the_application()
    {
        Assert.Throws<InvalidOperationException>(() => SecurityKeys.From(new SecurityOptions()));
        Assert.Throws<InvalidOperationException>(() => TestKeys.Create(("k1", Convert.ToBase64String(new byte[16]))));
        Assert.Throws<InvalidOperationException>(() => TestKeys.Create(TestKeys.KeyA, ("k1", TestKeys.KeyA)));
        Assert.Throws<InvalidOperationException>(() => SecurityKeys.From(new SecurityOptions
        {
            EncryptionKeys = { ["k1"] = TestKeys.KeyA },
            ActiveEncryptionKey = "k9",
            HashingKey = TestKeys.HashingKey,
        }));
    }
}
