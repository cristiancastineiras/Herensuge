namespace Herensuge.Modules.Identity.Application.Passkeys.GetPasskeys;

public sealed record GetPasskeysQuery(Guid UserId) : IQuery<IReadOnlyList<PasskeyResponse>>;

public sealed record PasskeyResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastUsedAtUtc { get; init; }
}
