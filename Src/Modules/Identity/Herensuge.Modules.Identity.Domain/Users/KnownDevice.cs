namespace Herensuge.Modules.Identity.Domain.Users;

/// <summary>A browser that already signed in. Only the hash of the device cookie is stored.</summary>
public sealed class KnownDevice
{
    private KnownDevice()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string DeviceIdHash { get; private set; } = string.Empty;

    public DateTime FirstSeenAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    internal static KnownDevice Create(Guid userId, string deviceIdHash, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        DeviceIdHash = deviceIdHash,
        FirstSeenAtUtc = utcNow,
        LastSeenAtUtc = utcNow,
    };

    internal void Touch(DateTime utcNow) => LastSeenAtUtc = utcNow;
}
