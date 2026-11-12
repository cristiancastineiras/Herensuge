namespace Herensuge.Modules.Identity.Domain.Tokens;

public interface IOneTimeTokenRepository
{
    Task<OneTimeToken?> GetByLinkHashAsync(OneTimeTokenPurpose purpose, string tokenHash, CancellationToken cancellationToken);

    Task<OneTimeToken?> GetLatestActiveAsync(Guid userId, OneTimeTokenPurpose purpose, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<OneTimeToken>> GetActiveAsync(Guid userId, OneTimeTokenPurpose purpose, DateTime utcNow, CancellationToken cancellationToken);

    void Add(OneTimeToken token);
}
