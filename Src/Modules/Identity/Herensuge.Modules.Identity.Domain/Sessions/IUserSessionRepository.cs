namespace Herensuge.Modules.Identity.Domain.Sessions;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> GetActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    void Add(UserSession session);
}
