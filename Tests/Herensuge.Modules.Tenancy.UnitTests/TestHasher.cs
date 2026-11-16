using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Domain;

namespace Herensuge.Modules.Tenancy.UnitTests;

/// <summary>Deterministic stand-in for the HMAC hasher of Common.Infrastructure.</summary>
internal sealed class TestHasher : ISecretHasher
{
    public string Hash(string purpose, string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{secret}")));
}
