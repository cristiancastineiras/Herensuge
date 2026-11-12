namespace Herensuge.Modules.Identity.Application.ExternalLogins.GetExternalLogins;

public sealed record GetExternalLoginsQuery(Guid UserId) : IQuery<IReadOnlyList<ExternalLoginResponse>>;

public sealed record ExternalLoginResponse
{
    public string Provider { get; init; } = string.Empty;

    public string? Email { get; init; }

    public DateTime LinkedAtUtc { get; init; }
}
