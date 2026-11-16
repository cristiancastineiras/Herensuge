using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.Roles;
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
        builder.Navigation(server => server.Permissions).AutoInclude();
        builder.Navigation(server => server.RoleTemplates).AutoInclude();
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
        builder.HasIndex(assignment => new { assignment.TenantId, assignment.UserId });
        builder.HasIndex(assignment => assignment.UserId);

        // Same schema, so foreign keys are allowed: deleting a role or an application role removes its assignments.
        builder.HasOne<Role>().WithMany().HasForeignKey(assignment => assignment.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoleTemplate>().WithMany().HasForeignKey(assignment => assignment.RoleTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
