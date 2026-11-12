namespace Herensuge.Modules.Identity.Domain.Users;

/// <summary>An account at Google or Microsoft. Identified by (provider, subject), never by email.</summary>
public sealed class ExternalLogin
{
    private ExternalLogin()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public DateTime LinkedAtUtc { get; private set; }

    internal static ExternalLogin Create(Guid userId, string provider, string subject, string? email, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Provider = provider,
        Subject = subject,
        Email = email,
        LinkedAtUtc = utcNow,
    };
}
