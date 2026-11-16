namespace Herensuge.Modules.Access.Application.Abstractions;

/// <summary>Commits every change of the Access module (and its outbox messages) in one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
