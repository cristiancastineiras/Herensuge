using Herensuge.Common.Application.Security;
using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Identity.Application.Passkeys.GetPasskeys;

internal sealed class GetPasskeysQueryHandler(IDbConnectionFactory dbConnectionFactory, IDataEncryptor encryptor)
    : IQueryHandler<GetPasskeysQuery, IReadOnlyList<PasskeyResponse>>
{
    private const string Sql =
        """
        SELECT id, name, created_at_utc, last_used_at_utc
        FROM identity.user_passkeys
        WHERE user_id = @UserId
        ORDER BY created_at_utc
        """;

    public async Task<Result<IReadOnlyList<PasskeyResponse>>> Handle(GetPasskeysQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<PasskeyResponse> passkeys = await connection.QueryAsync<PasskeyResponse>(
            new CommandDefinition(Sql, new { query.UserId }, cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<PasskeyResponse>>(
            [.. passkeys.Select(passkey => passkey with { Name = encryptor.Decrypt(passkey.Name, EncryptedColumns.PasskeyName) })]);
    }
}
