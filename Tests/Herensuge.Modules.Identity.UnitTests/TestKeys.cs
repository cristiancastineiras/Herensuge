using Herensuge.Common.Domain;
using Herensuge.Common.Infrastructure.Security;

namespace Herensuge.Modules.Identity.UnitTests;

/// <summary>Fixed key material for the tests (never used outside them).</summary>
internal static class TestKeys
{
    public static readonly string KeyA = Convert.ToBase64String([.. Enumerable.Range(0, 32).Select(value => (byte)value)]);

    public static readonly string KeyB = Convert.ToBase64String([.. Enumerable.Range(32, 32).Select(value => (byte)value)]);

    public static readonly string HashingKey = Convert.ToBase64String([.. Enumerable.Range(64, 32).Select(value => (byte)value)]);

    /// <summary>One encryption key (k1 = <see cref="KeyA"/>) and <see cref="HashingKey"/>.</summary>
    public static readonly SecurityKeys Keys = Create(("k1", KeyA));

    public static readonly ISecretHasher Hasher = new HmacSecretHasher(Keys);

    /// <summary>The last key is the active one; the hashing key is <see cref="HashingKey"/>.</summary>
    public static SecurityKeys Create(params (string Id, string Key)[] keys) => Create(HashingKey, keys);

    public static SecurityKeys Create(string hashingKey, params (string Id, string Key)[] keys)
    {
        var options = new SecurityOptions { ActiveEncryptionKey = keys[^1].Id, HashingKey = hashingKey };
        foreach ((string id, string key) in keys)
        {
            options.EncryptionKeys[id] = key;
        }

        return SecurityKeys.From(options);
    }
}
