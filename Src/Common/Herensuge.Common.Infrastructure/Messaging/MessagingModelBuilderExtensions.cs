using Microsoft.EntityFrameworkCore;

namespace Herensuge.Common.Infrastructure.Messaging;

public static class MessagingModelBuilderExtensions
{
    /// <summary>Maps outbox (optional) and inbox tables into the module schema.</summary>
    public static ModelBuilder ApplyMessagingConfiguration(this ModelBuilder modelBuilder, bool includeOutbox)
    {
        if (includeOutbox)
        {
            modelBuilder.Entity<OutboxMessage>(builder =>
            {
                builder.ToTable("outbox_messages");
                builder.HasKey(m => m.Id);
                builder.Property(m => m.Type).HasMaxLength(500);
                builder.Property(m => m.Content).HasColumnType("jsonb");
                builder.Property(m => m.CorrelationId).HasMaxLength(100);
                builder.Property(m => m.TraceParent).HasMaxLength(100);
                builder.Property(m => m.IpAddress).HasMaxLength(64);
                builder.Property(m => m.UserAgent).HasMaxLength(512);
                builder.HasIndex(m => m.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
            });

            modelBuilder.Entity<OutboxMessageConsumer>(builder =>
            {
                builder.ToTable("outbox_message_consumers");
                builder.HasKey(c => new { c.OutboxMessageId, c.Name });
                builder.Property(c => c.Name).HasMaxLength(500);
            });
        }

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(500);
            builder.Property(m => m.Content).HasColumnType("jsonb");
            builder.Property(m => m.CorrelationId).HasMaxLength(100);
            builder.Property(m => m.TraceParent).HasMaxLength(100);
            builder.Property(m => m.IpAddress).HasMaxLength(64);
            builder.Property(m => m.UserAgent).HasMaxLength(512);
            builder.HasIndex(m => m.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
        });

        modelBuilder.Entity<InboxMessageConsumer>(builder =>
        {
            builder.ToTable("inbox_message_consumers");
            builder.HasKey(c => new { c.InboxMessageId, c.Name });
            builder.Property(c => c.Name).HasMaxLength(500);
        });

        return modelBuilder;
    }
}
