using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Access.Infrastructure.Database;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options), IUnitOfWork
{
    internal DbSet<ResourceServer> ResourceServers => Set<ResourceServer>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AccessModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: true);
    }
}
