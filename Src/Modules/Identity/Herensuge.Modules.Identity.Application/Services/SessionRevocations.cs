namespace Herensuge.Modules.Identity.Application.Services;

internal static class SessionRevocations
{
    /// <summary>Revokes every active session of the user except <paramref name="keepSessionId"/>. The caller saves.</summary>
    public static async Task RevokeOthersAsync(
        this IUserSessionRepository sessions,
        Guid userId,
        Guid? keepSessionId,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        foreach (UserSession session in await sessions.GetActiveByUserAsync(userId, utcNow, cancellationToken))
        {
            if (session.Id != keepSessionId)
            {
                session.Revoke(reason, utcNow);
            }
        }
    }
}
