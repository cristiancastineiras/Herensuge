namespace Herensuge.Modules.Tenancy.Domain.Memberships;

public interface IMembershipRepository
{
    Task<Membership?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Membership>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> CountOwnersAsync(Guid tenantId, CancellationToken cancellationToken);

    void Add(Membership membership);

    void Remove(Membership membership);
}
