namespace Herensuge.Modules.Identity.Application.Users.GetCurrentUser;

public sealed record CurrentUserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public bool EmailVerified { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public bool HasPassword { get; init; }

    public bool TotpEnabled { get; init; }

    public int RecoveryCodesLeft { get; init; }

    public int PasskeyCount { get; init; }

    public string[] ExternalProviders { get; init; } = [];

    public bool IsPlatformAdmin { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
