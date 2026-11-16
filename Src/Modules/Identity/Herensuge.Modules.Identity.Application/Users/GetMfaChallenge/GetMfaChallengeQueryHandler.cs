using Herensuge.Common.Application.Security;
using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;

namespace Herensuge.Modules.Identity.Application.Users.GetMfaChallenge;

/// <summary>Same rules as User.GetMfaMethods, read with SQL.</summary>
internal sealed class GetMfaChallengeQueryHandler(IDbConnectionFactory dbConnectionFactory, IDataEncryptor encryptor)
    : IQueryHandler<GetMfaChallengeQuery, MfaChallengeResponse>
{
    private const string Sql =
        """
        SELECT u.totp_enabled_at_utc IS NOT NULL AS totp_enabled,
               u.phone_number,
               u.email,
               u.email_verified_at_utc IS NOT NULL AS email_verified,
               EXISTS (SELECT 1 FROM identity.user_recovery_codes c WHERE c.user_id = u.id AND c.used_at_utc IS NULL) AS has_recovery_codes
        FROM identity.users u
        WHERE u.id = @UserId
        """;

    public async Task<Result<MfaChallengeResponse>> Handle(GetMfaChallengeQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        Row? row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { query.UserId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return UserErrors.NotFound;
        }

        var methods = new List<string>(4);
        if (row.TotpEnabled)
        {
            methods.Add(MfaMethods.Totp);
        }

        if (row.PhoneNumber is not null)
        {
            methods.Add(MfaMethods.Sms);
        }

        bool email = (row.TotpEnabled || row.PhoneNumber is not null) && row.EmailVerified;
        if (email)
        {
            methods.Add(MfaMethods.Email);
        }

        if (row.HasRecoveryCodes)
        {
            methods.Add(MfaMethods.RecoveryCode);
        }

        string? phoneHint = row.PhoneNumber is null
            ? null
            : PhoneNumber.Mask(encryptor.Decrypt(row.PhoneNumber, EncryptedColumns.UserPhoneNumber));
        string? emailHint = email ? Email.Mask(encryptor.Decrypt(row.Email, EncryptedColumns.UserEmail)) : null;
        return new MfaChallengeResponse(methods, phoneHint, emailHint);
    }

    private sealed class Row
    {
        public bool TotpEnabled { get; init; }

        public string? PhoneNumber { get; init; }

        public string Email { get; init; } = string.Empty;

        public bool EmailVerified { get; init; }

        public bool HasRecoveryCodes { get; init; }
    }
}
