namespace Herensuge.Modules.Tenancy.Application.Abstractions;

/// <summary>Commits every change of the Tenancy module (and its outbox messages) in one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
