using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Access.Infrastructure.Relationships;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Access.Infrastructure.Database;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options), IUnitOfWork
{
    internal DbSet<ResourceServer> ResourceServers => Set<ResourceServer>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    internal DbSet<Group> Groups => Set<Group>();

    internal DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();

    internal DbSet<Plan> Plans => Set<Plan>();

    internal DbSet<TenantPlan> TenantPlans => Set<TenantPlan>();

    internal DbSet<RelationshipStoreSettings> RelationshipStores => Set<RelationshipStoreSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AccessModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: true);
    }
}
