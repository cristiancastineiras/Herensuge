using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Identity.Application.Abstractions;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Infrastructure.Security;

/// <summary>Defaults are the OWASP minimum for Argon2id (19 MiB, t=2, p=1). Tune them with a load test.</summary>
public sealed class Argon2Options
{
    public const string SectionName = "Identity:Argon2";

    public int MemorySizeKiB { get; set; } = 19456;

    public int Iterations { get; set; } = 2;

    public int Parallelism { get; set; } = 1;
}

/// <summary>
/// Argon2id in PHC string format: <c>$argon2id$v=19$m=19456,t=2,p=1,k=1$salt$hash</c>. Every hash has its own random
/// 128-bit salt and also uses a pepper (Argon2id "secret" input, derived from Security:HashingKey), which lives
/// outside the database: stolen hashes cannot be cracked offline without it. <c>k=1</c> marks the pepper version;
/// hashes without it (created before the pepper) still verify and are rehashed at the next sign-in.
/// </summary>
internal sealed class Argon2PasswordHasher(IOptions<Argon2Options> options, SecurityKeys keys) : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int PepperVersion = 1;

    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSize);

    public string Hash(string password)
    {
        Argon2Options settings = options.Value;
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Compute(password, salt, keys.PasswordPepper, settings.MemorySizeKiB, settings.Iterations, settings.Parallelism, HashSize);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={settings.MemorySizeKiB},t={settings.Iterations},p={settings.Parallelism},k={PepperVersion}${ToBase64(salt)}${ToBase64(hash)}");
    }

    public PasswordVerificationResult Verify(string passwordHash, string password)
    {
        if (!TryParse(passwordHash, out ParsedHash parsed))
        {
            return PasswordVerificationResult.Failed;
        }

        byte[]? pepper = parsed.PepperVersion == PepperVersion ? keys.PasswordPepper : null;
        if (parsed.PepperVersion != 0 && pepper is null)
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] actual = Compute(password, parsed.Salt, pepper, parsed.MemorySizeKiB, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, parsed.Hash))
        {
            return PasswordVerificationResult.Failed;
        }

        Argon2Options settings = options.Value;
        bool current = parsed.MemorySizeKiB == settings.MemorySizeKiB
            && parsed.Iterations == settings.Iterations
            && parsed.Parallelism == settings.Parallelism
            && parsed.PepperVersion == PepperVersion;

        return current ? PasswordVerificationResult.Success : PasswordVerificationResult.SuccessRehashNeeded;
    }

    public void SimulateVerification(string password)
    {
        Argon2Options settings = options.Value;
        _ = Compute(password, DummySalt, keys.PasswordPepper, settings.MemorySizeKiB, settings.Iterations, settings.Parallelism, HashSize);
    }

    private static byte[] Compute(string password, byte[] salt, byte[]? pepper, int memorySizeKiB, int iterations, int parallelism, int length)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            KnownSecret = pepper,
            MemorySize = memorySizeKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(length);
    }

    private static bool TryParse(string encoded, out ParsedHash parsed)
    {
        parsed = default;

        // ["", "argon2id", "v=19", "m=..,t=..,p=..[,k=..]", salt, hash]
        string[] parts = encoded.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19")
        {
            return false;
        }

        int memory = 0, iterations = 0, parallelism = 0, pepperVersion = 0;
        foreach (string parameter in parts[3].Split(','))
        {
            string[] pair = parameter.Split('=');
            if (pair.Length != 2 || !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                return false;
            }

            switch (pair[0])
            {
                case "m": memory = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                case "k": pepperVersion = value; break;
                default: return false;
            }
        }

        try
        {
            parsed = new ParsedHash(memory, iterations, parallelism, pepperVersion, FromBase64(parts[4]), FromBase64(parts[5]));
            return memory > 0 && iterations > 0 && parallelism > 0 && parsed.Hash.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // PHC strings use unpadded standard base64.
    private static string ToBase64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] FromBase64(string value) =>
        Convert.FromBase64String(value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '='));

    private readonly record struct ParsedHash(int MemorySizeKiB, int Iterations, int Parallelism, int PepperVersion, byte[] Salt, byte[] Hash);
}
