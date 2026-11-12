using System.Data.Common;
using System.Text.Json;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Modules.Audit.Application.Abstractions;

namespace Herensuge.Modules.Audit.Application.Entries.GetAuditEntries;

internal sealed class GetAuditEntriesQueryHandler(IDbConnectionFactory dbConnectionFactory, IDataEncryptor encryptor)
    : IQueryHandler<GetAuditEntriesQuery, AuditPage>
{
    private const string Sql =
        """
        SELECT id, occurred_at_utc, action, tenant_id, subject_user_id, actor_user_id, ip_address, user_agent, metadata
        FROM audit.audit_entries
        WHERE (@TenantId::uuid IS NULL OR tenant_id = @TenantId)
          AND (@UserId::uuid IS NULL OR subject_user_id = @UserId OR actor_user_id = @UserId)
          AND (@Action::text IS NULL OR action = @Action)
          AND (@Before::timestamptz IS NULL OR occurred_at_utc < @Before)
        ORDER BY occurred_at_utc DESC
        LIMIT @Limit
        """;

    public async Task<Result<AuditPage>> Handle(GetAuditEntriesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        List<Row> rows = [.. await connection.QueryAsync<Row>(new CommandDefinition(
            Sql,
            new
            {
                query.TenantId,
                query.UserId,
                query.Action,
                Before = query.Before is { } before ? DateTime.SpecifyKind(before, DateTimeKind.Utc) : (DateTime?)null,
                Limit = query.PageSize + 1,
            },
            cancellationToken: cancellationToken))];

        bool hasMore = rows.Count > query.PageSize;
        List<AuditEntryResponse> items = [.. rows.Take(query.PageSize).Select(row => new AuditEntryResponse(
            row.Id,
            row.OccurredAtUtc,
            row.Action,
            row.TenantId,
            row.SubjectUserId,
            row.ActorUserId,
            encryptor.DecryptNullable(row.IpAddress, EncryptedColumns.IpAddress),
            encryptor.DecryptNullable(row.UserAgent, EncryptedColumns.UserAgent),
            JsonDocument.Parse(encryptor.Decrypt(row.Metadata, EncryptedColumns.Metadata)).RootElement.Clone()))];

        return new AuditPage(items, hasMore ? items[^1].OccurredAtUtc : null);
    }

    private sealed class Row
    {
        public Guid Id { get; init; }

        public DateTime OccurredAtUtc { get; init; }

        public string Action { get; init; } = string.Empty;

        public Guid? TenantId { get; init; }

        public Guid? SubjectUserId { get; init; }

        public Guid? ActorUserId { get; init; }

        public string? IpAddress { get; init; }

        public string? UserAgent { get; init; }

        public string Metadata { get; init; } = string.Empty;
    }
}
