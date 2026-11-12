using Herensuge.Common.Application.Security;
using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Tenancy.Application.Invitations.GetInvitations;

internal sealed class GetInvitationsQueryHandler(IDbConnectionFactory dbConnectionFactory, IDataEncryptor encryptor, TimeProvider timeProvider)
    : IQueryHandler<GetInvitationsQuery, IReadOnlyList<InvitationResponse>>
{
    private const string Sql =
        """
        SELECT id, email, lower(role) AS role, invited_by_user_id, created_at_utc, expires_at_utc
        FROM tenancy.invitations
        WHERE tenant_id = @TenantId AND status = 'Pending' AND expires_at_utc > @Now
        ORDER BY created_at_utc DESC
        """;

    public async Task<Result<IReadOnlyList<InvitationResponse>>> Handle(GetInvitationsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<InvitationResponse> invitations = await connection.QueryAsync<InvitationResponse>(new CommandDefinition(
            Sql,
            new { query.TenantId, Now = timeProvider.GetUtcNow().UtcDateTime },
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<InvitationResponse>>(
            [.. invitations.Select(invitation => invitation with { Email = encryptor.Decrypt(invitation.Email, EncryptedColumns.InvitationEmail) })]);
    }
}
