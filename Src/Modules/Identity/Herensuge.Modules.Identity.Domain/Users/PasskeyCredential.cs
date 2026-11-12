namespace Herensuge.Modules.Identity.Domain.Users;

public sealed class PasskeyCredential
{
    public const int MaxNameLength = 100;

    private PasskeyCredential()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] CredentialId { get; private set; } = [];

    public byte[] PublicKey { get; private set; } = [];

    public long SignCount { get; private set; }

    public Guid Aaguid { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? LastUsedAtUtc { get; private set; }

    internal static PasskeyCredential Create(
        Guid userId,
        byte[] credentialId,
        byte[] publicKey,
        long signCount,
        Guid aaguid,
        string name,
        DateTime utcNow) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignCount = signCount,
            Aaguid = aaguid,
            Name = NormalizeName(name),
            CreatedAtUtc = utcNow,
        };

    internal void Rename(string name) => Name = NormalizeName(name);

    internal void RecordUse(long signCount, DateTime utcNow)
    {
        SignCount = signCount;
        LastUsedAtUtc = utcNow;
    }

    private static string NormalizeName(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "Passkey";
        }

        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }
}
