using Herensuge.Modules.Access.Domain.Groups;
using Herensuge.Modules.Access.Domain.Plans;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Herensuge.Modules.Access.Infrastructure.Database.Configurations;

internal sealed class ResourceServerConfiguration : IEntityTypeConfiguration<ResourceServer>
{
    public void Configure(EntityTypeBuilder<ResourceServer> builder)
    {
        builder.ToTable("resource_servers");
        builder.HasKey(server => server.Id);
        builder.Property(server => server.Id).ValueGeneratedNever();
        builder.Property(server => server.Key).HasMaxLength(50);
        builder.HasIndex(server => server.Key).IsUnique();
        builder.Property(server => server.Name).HasMaxLength(100);
        builder.Property(server => server.ClientId).HasMaxLength(100);
        builder.HasIndex(server => server.ClientId).IsUnique();
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(server => server.ActivePermissions);

        builder.HasMany(server => server.Permissions).WithOne().HasForeignKey(permission => permission.ResourceServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(server => server.RoleTemplates).WithOne().HasForeignKey(template => template.ResourceServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(server => server.Features).WithOne().HasForeignKey(feature => feature.ResourceServerId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(server => server.Permissions).AutoInclude();
        builder.Navigation(server => server.RoleTemplates).AutoInclude();
        builder.Navigation(server => server.Features).AutoInclude();
    }
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");
        builder.HasKey(feature => feature.Id);
        builder.Property(feature => feature.Id).ValueGeneratedNever();
        builder.Property(feature => feature.Key).HasMaxLength(50);
        builder.Property(feature => feature.Name).HasMaxLength(100);
        builder.HasIndex(feature => new { feature.ResourceServerId, feature.Key }).IsUnique();
    }
}

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();
        builder.Property(group => group.Name).HasMaxLength(Group.MaxNameLength);
        builder.Property(group => group.Description).HasMaxLength(500);
        builder.HasIndex(group => group.TenantId);

        // GIN: "groups of this user" is an array containment query.
        builder.HasIndex(group => group.MemberUserIds).HasMethod("gin");
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class ServiceAccountConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        builder.ToTable("service_accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.Name).HasMaxLength(ServiceAccount.MaxNameLength);
        builder.Property(account => account.ClientId).HasMaxLength(100);
        builder.HasIndex(account => account.ClientId).IsUnique();
        builder.HasIndex(account => account.TenantId);
    }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.Key).HasMaxLength(50);
        builder.HasIndex(plan => plan.Key).IsUnique();
        builder.Property(plan => plan.Name).HasMaxLength(100);
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class TenantPlanConfiguration : IEntityTypeConfiguration<TenantPlan>
{
    public void Configure(EntityTypeBuilder<TenantPlan> builder)
    {
        builder.ToTable("tenant_plans");
        builder.HasKey(tenantPlan => tenantPlan.TenantId);
        builder.Property(tenantPlan => tenantPlan.TenantId).ValueGeneratedNever();

        // Deleting a plan sends its organizations back to the default one.
        builder.HasOne<Plan>().WithMany().HasForeignKey(tenantPlan => tenantPlan.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id).ValueGeneratedNever();
        builder.Property(permission => permission.Key).HasMaxLength(100);
        builder.Property(permission => permission.Description).HasMaxLength(500);
        builder.Property(permission => permission.Requires).HasMaxLength(20);
        builder.Property(permission => permission.FeatureKey).HasMaxLength(50);
        builder.HasIndex(permission => new { permission.ResourceServerId, permission.Key }).IsUnique();
    }
}

internal sealed class RoleTemplateConfiguration : IEntityTypeConfiguration<RoleTemplate>
{
    public void Configure(EntityTypeBuilder<RoleTemplate> builder)
    {
        builder.ToTable("role_templates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedNever();
        builder.Property(template => template.Key).HasMaxLength(50);
        builder.Property(template => template.Name).HasMaxLength(100);
        builder.Property(template => template.Description).HasMaxLength(500);
        builder.HasIndex(template => new { template.ResourceServerId, template.Key }).IsUnique();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(Role.MaxNameLength);
        builder.Property(role => role.Description).HasMaxLength(500);
        builder.HasIndex(role => role.TenantId);
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("role_assignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();
        builder.Property(assignment => assignment.SubjectType).HasMaxLength(10).HasDefaultValue(SubjectTypes.User);

        // The subject is polymorphic (user, group or service account): no foreign key; groups and accounts remove theirs.
        builder.HasIndex(assignment => new { assignment.TenantId, assignment.SubjectId });
        builder.HasIndex(assignment => assignment.SubjectId);

        // Same schema, so foreign keys are allowed: deleting a role or an application role removes its assignments.
        builder.HasOne<Role>().WithMany().HasForeignKey(assignment => assignment.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoleTemplate>().WithMany().HasForeignKey(assignment => assignment.RoleTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RelationshipStoreSettingsConfiguration : IEntityTypeConfiguration<Relationships.RelationshipStoreSettings>
{
    public void Configure(EntityTypeBuilder<Relationships.RelationshipStoreSettings> builder)
    {
        builder.ToTable("relationship_store");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.StoreId).HasMaxLength(64);
        builder.Property(settings => settings.ModelId).HasMaxLength(64);
        builder.Property(settings => settings.ModelHash).HasMaxLength(64);
    }
}

internal sealed class AccessRequestConfiguration : IEntityTypeConfiguration<Domain.Requests.AccessRequest>
{
    public void Configure(EntityTypeBuilder<Domain.Requests.AccessRequest> builder)
    {
        builder.ToTable("access_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Reason).HasMaxLength(Domain.Requests.AccessRequest.MaxReasonLength);
        builder.Property(request => request.Status).HasMaxLength(10);
        builder.HasIndex(request => new { request.TenantId, request.UserId });
        builder.Property<uint>("Version").IsRowVersion();

        // Deleting the role or the application role removes the requests for it.
        builder.HasOne<Role>().WithMany().HasForeignKey(request => request.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoleTemplate>().WithMany().HasForeignKey(request => request.RoleTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
