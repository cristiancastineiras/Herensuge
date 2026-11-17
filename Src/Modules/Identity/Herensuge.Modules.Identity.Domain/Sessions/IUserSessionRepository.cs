namespace Herensuge.Modules.Identity.Domain.Sessions;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> GetActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>Support sessions past their 30 minutes and not ended yet (plan D11).</summary>
    Task<IReadOnlyList<UserSession>> GetExpiredSupportAsync(DateTime utcNow, CancellationToken cancellationToken);

    void Add(UserSession session);
}
