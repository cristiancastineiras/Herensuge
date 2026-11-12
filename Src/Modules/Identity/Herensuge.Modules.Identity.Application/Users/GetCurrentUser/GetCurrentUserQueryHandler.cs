using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Identity.Application.Users.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private const string Sql =
        """
        SELECT u.id,
               u.email,
               u.email_verified_at_utc IS NOT NULL AS email_verified,
               u.display_name,
               u.phone_number,
               u.password_hash IS NOT NULL AS has_password,
               u.totp_enabled_at_utc IS NOT NULL AS totp_enabled,
               (SELECT count(*) FROM identity.user_recovery_codes c WHERE c.user_id = u.id AND c.used_at_utc IS NULL)::int AS recovery_codes_left,
               (SELECT count(*) FROM identity.user_passkeys p WHERE p.user_id = u.id)::int AS passkey_count,
               ARRAY(SELECT l.provider FROM identity.user_external_logins l WHERE l.user_id = u.id ORDER BY l.provider) AS external_providers,
               u.is_platform_admin,
               u.created_at_utc
        FROM identity.users u
        WHERE u.id = @UserId
        """;

    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        CurrentUserResponse? user = await connection.QuerySingleOrDefaultAsync<CurrentUserResponse>(
            new CommandDefinition(Sql, new { query.UserId }, cancellationToken: cancellationToken));

        return user is null ? UserErrors.NotFound : user;
    }
}
