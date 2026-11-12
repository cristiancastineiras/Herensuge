namespace Herensuge.Modules.Tenancy.Domain.Invitations;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<Invitation>> GetPendingByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken);

    void Add(Invitation invitation);
}
