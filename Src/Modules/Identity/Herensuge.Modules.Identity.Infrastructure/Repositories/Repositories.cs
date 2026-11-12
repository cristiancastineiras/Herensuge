using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;
using Herensuge.Modules.Identity.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    // The aggregate always loads its four child collections: split queries avoid a cartesian product.
    private IQueryable<User> Users => context.Users.AsSplitQuery();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    // The email column is encrypted: users are found through its blind index.
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        string index = context.IndexEmail(email);
        return Users.SingleOrDefaultAsync(user => EF.Property<string>(user, IdentityDbContext.EmailIndex) == index, cancellationToken);
    }

    public Task<User?> GetByExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken) =>
        Users.SingleOrDefaultAsync(
            user => user.ExternalLogins.Any(login => login.Provider == provider && login.Subject == subject),
            cancellationToken);

    public Task<User?> GetByPasskeyCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken) =>
        Users.SingleOrDefaultAsync(
            user => user.Passkeys.Any(passkey => passkey.CredentialId == credentialId),
            cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        string index = context.IndexEmail(email);
        return context.Users.AnyAsync(user => EF.Property<string>(user, IdentityDbContext.EmailIndex) == index, cancellationToken);
    }

    public Task<bool> PasskeyCredentialExistsAsync(byte[] credentialId, CancellationToken cancellationToken) =>
        context.Set<PasskeyCredential>().AnyAsync(passkey => passkey.CredentialId == credentialId, cancellationToken);

    public void Add(User user) => context.Users.Add(user);

    public void Remove(User user) => context.Users.Remove(user);
}

internal sealed class UserSessionRepository(IdentityDbContext context) : IUserSessionRepository
{
    public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.UserSessions.SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserSession>> GetActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        await context.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

    public void Add(UserSession session) => context.UserSessions.Add(session);
}

internal sealed class OneTimeTokenRepository(IdentityDbContext context) : IOneTimeTokenRepository
{
    public Task<OneTimeToken?> GetByLinkHashAsync(OneTimeTokenPurpose purpose, string tokenHash, CancellationToken cancellationToken) =>
        context.OneTimeTokens.SingleOrDefaultAsync(token => token.Purpose == purpose && token.TokenHash == tokenHash, cancellationToken);

    public Task<OneTimeToken?> GetLatestActiveAsync(Guid userId, OneTimeTokenPurpose purpose, DateTime utcNow, CancellationToken cancellationToken) =>
        Active(userId, purpose, utcNow)
            .OrderByDescending(token => token.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<OneTimeToken>> GetActiveAsync(Guid userId, OneTimeTokenPurpose purpose, DateTime utcNow, CancellationToken cancellationToken) =>
        await Active(userId, purpose, utcNow).ToListAsync(cancellationToken);

    public void Add(OneTimeToken token) => context.OneTimeTokens.Add(token);

    private IQueryable<OneTimeToken> Active(Guid userId, OneTimeTokenPurpose purpose, DateTime utcNow) =>
        context.OneTimeTokens.Where(token =>
            token.UserId == userId
            && token.Purpose == purpose
            && token.ConsumedAtUtc == null
            && token.ExpiresAtUtc > utcNow
            && token.FailedAttempts < OneTimeToken.MaxFailedAttempts);
}
