using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.UnitTests;

public sealed class Argon2PasswordHasherTests
{
    private static readonly Argon2Options Fast = new() { MemorySizeKiB = 8192, Iterations = 1, Parallelism = 1 };

    [Fact]
    public void Hash_uses_the_phc_format_and_verifies()
    {
        var hasher = new Argon2PasswordHasher(Options.Create(Fast));

        string hash = hasher.Hash("correct horse battery staple");

        Assert.StartsWith("$argon2id$v=19$m=8192,t=1,p=1$", hash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(hash, "correct horse battery staple"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(hash, "Correct horse battery staple"));
    }

    [Fact]
    public void Hashes_are_salted()
    {
        var hasher = new Argon2PasswordHasher(Options.Create(Fast));

        Assert.NotEqual(hasher.Hash("same password!"), hasher.Hash("same password!"));
    }

    [Fact]
    public void Hashes_with_old_parameters_ask_for_a_rehash()
    {
        string oldHash = new Argon2PasswordHasher(Options.Create(Fast)).Hash("correct horse battery staple");
        var current = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemorySizeKiB = 9216, Iterations = 1, Parallelism = 1 }));

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, current.Verify(oldHash, "correct horse battery staple"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("$argon2i$v=19$m=8192,t=1,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=x,t=1,p=1$c2FsdA$aGFzaA")]
    public void Malformed_hashes_never_verify(string hash)
    {
        var hasher = new Argon2PasswordHasher(Options.Create(Fast));

        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(hash, "anything at all"));
    }
}
