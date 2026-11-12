using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Tenancy.Application.Invitations.GetInvitationPreview;

internal sealed class GetInvitationPreviewQueryHandler(IDbConnectionFactory dbConnectionFactory, TimeProvider timeProvider)
    : IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewResponse>
{
    private const string Sql =
        """
        SELECT t.name AS tenant_name, i.email, lower(i.role) AS role, i.status, i.expires_at_utc, t.is_active AS tenant_is_active
        FROM tenancy.invitations i
        JOIN tenancy.tenants t ON t.id = i.tenant_id
        WHERE i.token_hash = @TokenHash
        """;

    public async Task<Result<InvitationPreviewResponse>> Handle(GetInvitationPreviewQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        Row? row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            Sql,
            new { TokenHash = Invitation.HashToken(query.Token) },
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return InvitationErrors.NotFound;
        }

        bool isValid = row.TenantIsActive
            && row.Status == nameof(InvitationStatus.Pending)
            && row.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime;

        return new InvitationPreviewResponse(row.TenantName, row.Email, row.Role, isValid, row.ExpiresAtUtc);
    }

    private sealed class Row
    {
        public string TenantName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public bool TenantIsActive { get; init; }
    }
}
