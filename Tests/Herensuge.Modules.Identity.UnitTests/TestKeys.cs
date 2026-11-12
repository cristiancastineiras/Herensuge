using Herensuge.Common.Domain;
using Herensuge.Common.Infrastructure.Security;

namespace Herensuge.Modules.Identity.UnitTests;

/// <summary>Fixed test keys (never use outside tests).</summary>
internal static class TestKeys
{
    public static readonly string KeyA = Convert.ToBase64String(Enumerable.Repeat((byte)0xA1, 32).ToArray());
    public static readonly string KeyB = Convert.ToBase64String(Enumerable.Repeat((byte)0xB2, 32).ToArray());
    public static readonly string HashingKey = Convert.ToBase64String(Enumerable.Repeat((byte)0xC3, 32).ToArray());

    public static readonly SecurityKeys Keys = Create(("k1", KeyA));

    public static readonly ISecretHasher Hasher = new HmacSecretHasher(Keys);

    /// <summary>The last key is the active one.</summary>
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
