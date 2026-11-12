using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Identity.Application.ExternalLogins.GetExternalLogins;

internal sealed class GetExternalLoginsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetExternalLoginsQuery, IReadOnlyList<ExternalLoginResponse>>
{
    private const string Sql =
        """
        SELECT provider, email, linked_at_utc
        FROM identity.user_external_logins
        WHERE user_id = @UserId
        ORDER BY provider
        """;

    public async Task<Result<IReadOnlyList<ExternalLoginResponse>>> Handle(GetExternalLoginsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<ExternalLoginResponse> logins = await connection.QueryAsync<ExternalLoginResponse>(
            new CommandDefinition(Sql, new { query.UserId }, cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<ExternalLoginResponse>>([.. logins]);
    }
}
