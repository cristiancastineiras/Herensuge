namespace Herensuge.Modules.Identity.Domain.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken);

    Task<User?> GetByPasskeyCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> PasskeyCredentialExistsAsync(byte[] credentialId, CancellationToken cancellationToken);

    void Add(User user);

    void Remove(User user);
}
