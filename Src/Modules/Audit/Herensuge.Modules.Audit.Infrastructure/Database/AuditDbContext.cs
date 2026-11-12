using Herensuge.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Audit.Infrastructure.Database;

/// <summary>Persistence model of the audit log (append-only, written with SQL by <see cref="AuditLog"/>).</summary>
public sealed class AuditEntry
{
    public Guid Id { get; init; }

    public Guid EventId { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public required string Action { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? SubjectUserId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public required string Metadata { get; init; }
}

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditModule.Schema);

        modelBuilder.Entity<AuditEntry>(builder =>
        {
            builder.ToTable("audit_entries");
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id).ValueGeneratedNever();
            builder.Property(entry => entry.Action).HasMaxLength(100);
            builder.Property(entry => entry.IpAddress).HasMaxLength(64);
            builder.Property(entry => entry.UserAgent).HasMaxLength(512);
            builder.Property(entry => entry.Metadata).HasColumnType("jsonb");
            builder.HasIndex(entry => entry.EventId).IsUnique();
            builder.HasIndex(entry => entry.OccurredAtUtc);
            builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc });
            builder.HasIndex(entry => new { entry.SubjectUserId, entry.OccurredAtUtc });
        });

        modelBuilder.ApplyMessagingConfiguration(includeOutbox: false);
    }
}
