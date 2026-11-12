using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Application.Sessions.ValidateSession;
using Herensuge.Modules.Identity.Application.SignIn.SignOut;
using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Identity.Infrastructure.PublicApi;

internal sealed class IdentityModuleApi(
    IDbConnectionFactory dbConnectionFactory,
    ICommandHandler<ValidateSessionCommand> validateSession,
    ICommandHandler<SignOutCommand> signOut) : IIdentityModuleApi
{
    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await signOut.Handle(new SignOutCommand(sessionId), cancellationToken);

    public async Task<UserSnapshot?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            """
            SELECT id, email, email_verified_at_utc IS NOT NULL AS email_verified, display_name, is_platform_admin,
                   (totp_enabled_at_utc IS NOT NULL OR phone_number IS NOT NULL) AS has_mfa
            FROM identity.users
            WHERE id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new UserSnapshot(row.Id, row.Email, row.EmailVerified, row.DisplayName, row.IsPlatformAdmin, row.HasMfa);
    }

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<UserRow> rows = await connection.QueryAsync<UserRow>(new CommandDefinition(
            "SELECT id, email, display_name FROM identity.users WHERE id = ANY(@Ids)",
            new { Ids = userIds.ToArray() },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new UserSummary(row.Id, row.Email, row.DisplayName))];
    }

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM identity.users WHERE email = @Email",
            new { Email = email },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        Result result = await validateSession.Handle(new ValidateSessionCommand(sessionId, userId), cancellationToken);
        return result.IsSuccess;
    }

    private sealed class UserRow
    {
        public Guid Id { get; init; }

        public string Email { get; init; } = string.Empty;

        public bool EmailVerified { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public bool IsPlatformAdmin { get; init; }

        public bool HasMfa { get; init; }
    }
}
