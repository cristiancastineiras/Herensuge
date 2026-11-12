using Herensuge.Common.Application.Security;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Tenancy.Application.Abstractions;
using Herensuge.Modules.Tenancy.Domain.Invitations;
using Herensuge.Modules.Tenancy.Domain.Memberships;
using Herensuge.Modules.Tenancy.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Tenancy.Infrastructure.Database;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, IDataEncryptor encryptor)
    : DbContext(options), IUnitOfWork
{
    internal DbSet<Tenant> Tenants => Set<Tenant>();

    internal DbSet<Membership> Memberships => Set<Membership>();

    internal DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TenancyModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: true);
        modelBuilder.UseDataEncryption(encryptor);
    }
}
