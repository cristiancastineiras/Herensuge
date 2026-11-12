using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Tenancy.Application.Abstractions;
using Herensuge.Modules.Tenancy.Domain.Invitations;
using Herensuge.Modules.Tenancy.Domain.Memberships;
using Herensuge.Modules.Tenancy.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Herensuge.Modules.Tenancy.Infrastructure.Database.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();
        builder.Property(tenant => tenant.Name).HasMaxLength(Tenant.MaxNameLength);
        builder.Property(tenant => tenant.Slug).HasMaxLength(TenantSlug.MaxLength);
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(membership => new { membership.TenantId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => membership.UserId);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(membership => membership.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();
        builder.Property(invitation => invitation.Email).IsEncrypted(EncryptedColumns.InvitationEmail);
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Status });
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(invitation => invitation.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
