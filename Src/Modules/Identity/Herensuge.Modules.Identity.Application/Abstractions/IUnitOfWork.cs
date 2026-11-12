namespace Herensuge.Modules.Identity.Application.Abstractions;

/// <summary>Commits every change of the Identity module (and its outbox messages) in one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
