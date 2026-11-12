using Dapper;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Audit.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Herensuge.Modules.Audit.Infrastructure;

/// <summary>Append-only log. IP address, user agent and metadata are encrypted.</summary>
internal sealed class AuditLog(NpgsqlDataSource dataSource, IDataEncryptor encryptor) : IAuditLog
{
    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit.audit_entries
                (id, event_id, occurred_at_utc, action, tenant_id, subject_user_id, actor_user_id, ip_address, user_agent, metadata)
            VALUES
                (@Id, @EventId, @OccurredAtUtc, @Action, @TenantId, @SubjectUserId, @ActorUserId, @IpAddress, @UserAgent, @Metadata)
            ON CONFLICT (event_id) DO NOTHING
            """,
            new
            {
                Id = Guid.CreateVersion7(),
                record.EventId,
                OccurredAtUtc = DateTime.SpecifyKind(record.OccurredAtUtc, DateTimeKind.Utc),
                record.Action,
                record.TenantId,
                record.SubjectUserId,
                record.ActorUserId,
                IpAddress = encryptor.EncryptNullable(record.IpAddress, EncryptedColumns.IpAddress),
                UserAgent = encryptor.EncryptNullable(record.UserAgent, EncryptedColumns.UserAgent),
                Metadata = encryptor.Encrypt(record.Metadata, EncryptedColumns.Metadata),
            },
            cancellationToken: cancellationToken));
    }
}

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Entries older than this are deleted daily. 0 keeps them forever.</summary>
    public int RetentionDays { get; set; } = 365;
}

internal sealed class AuditRetentionJob(
    NpgsqlDataSource dataSource,
    IOptions<AuditOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditRetentionJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int retentionDays = options.Value.RetentionDays;
            if (retentionDays <= 0)
            {
                continue;
            }

            try
            {
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(stoppingToken);
                int deleted = await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM audit.audit_entries WHERE occurred_at_utc < @Cutoff",
                    new { Cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays) },
                    cancellationToken: stoppingToken));
                logger.LogInformation("Audit retention removed {Count} entries older than {Days} days", deleted, retentionDays);
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                logger.LogWarning(exception, "Audit retention failed; it will run again tomorrow");
            }
        }
    }
}
