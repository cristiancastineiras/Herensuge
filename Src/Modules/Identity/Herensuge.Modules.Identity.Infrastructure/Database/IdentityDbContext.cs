using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Identity.Infrastructure.Database;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IUnitOfWork
{
    internal DbSet<User> Users => Set<User>();

    internal DbSet<UserSession> UserSessions => Set<UserSession>();

    internal DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentityModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: true);
    }
}
