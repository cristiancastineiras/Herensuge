using Herensuge.Common.Application.Security;
using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Identity.Application.Sessions.GetSessions;

internal sealed class GetSessionsQueryHandler(IDbConnectionFactory dbConnectionFactory, IDataEncryptor encryptor, TimeProvider timeProvider)
    : IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionResponse>>
{
    private const string Sql =
        """
        SELECT id, authentication_methods, authenticated_at_utc, created_at_utc, last_seen_at_utc,
               expires_at_utc, ip_address, user_agent
        FROM identity.user_sessions
        WHERE user_id = @UserId AND revoked_at_utc IS NULL AND expires_at_utc > @Now
        ORDER BY last_seen_at_utc DESC
        """;

    public async Task<Result<IReadOnlyList<SessionResponse>>> Handle(GetSessionsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<SessionResponse> sessions = await connection.QueryAsync<SessionResponse>(new CommandDefinition(
            Sql,
            new { query.UserId, Now = timeProvider.GetUtcNow().UtcDateTime },
            cancellationToken: cancellationToken));

        return Result.Success<IReadOnlyList<SessionResponse>>(
            [.. sessions.Select(session => session with
            {
                IpAddress = encryptor.DecryptNullable(session.IpAddress, EncryptedColumns.SessionIpAddress),
                UserAgent = encryptor.DecryptNullable(session.UserAgent, EncryptedColumns.SessionUserAgent),
                IsCurrent = session.Id == query.CurrentSessionId,
            })]);
    }
}
