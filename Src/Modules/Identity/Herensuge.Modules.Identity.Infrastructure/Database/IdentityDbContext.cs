using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Herensuge.Modules.Identity.Infrastructure.Database;

/// <summary>
/// Identity schema. Personal data is encrypted column by column (see the configurations); the email is found
/// through its blind index (<see cref="EmailIndex"/>), kept up to date on every save.
/// </summary>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IDataEncryptor encryptor,
    ISecretHasher hasher) : DbContext(options), IUnitOfWork
{
    /// <summary>Shadow property: keyed hash of the normalized email (unique).</summary>
    internal const string EmailIndex = "EmailIndex";

    internal DbSet<User> Users => Set<User>();

    internal DbSet<UserSession> UserSessions => Set<UserSession>();

    internal DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    /// <summary>Value to compare with <see cref="EmailIndex"/> when looking a user up by email.</summary>
    internal string IndexEmail(string email) =>
        hasher.Hash(EncryptedColumns.UserEmailIndex, email.Trim().ToLowerInvariant());

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateEmailIndexes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateEmailIndexes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentityModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: true);
        modelBuilder.UseDataEncryption(encryptor);
    }

    private void UpdateEmailIndexes()
    {
        foreach (EntityEntry<User> entry in ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added || (entry.State == EntityState.Modified && entry.Property(user => user.Email).IsModified))
            {
                entry.Property<string>(EmailIndex).CurrentValue = IndexEmail(entry.Entity.Email);
            }
        }
    }
}
