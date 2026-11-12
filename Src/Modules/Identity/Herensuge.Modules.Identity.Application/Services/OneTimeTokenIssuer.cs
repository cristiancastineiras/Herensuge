using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;

namespace Herensuge.Modules.Identity.Application.Services;

/// <summary>Issues a new token, invalidating the previous ones of the same purpose. At most one every 30 seconds.</summary>
public sealed class OneTimeTokenIssuer(IOneTimeTokenRepository tokens, ISecretHasher hasher, TimeProvider timeProvider)
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);

    public async Task<Result> IssueAsync(Guid userId, OneTimeTokenPurpose purpose, string? target, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<OneTimeToken> active = await tokens.GetActiveAsync(userId, purpose, now, cancellationToken);
        if (active.Any(token => now - token.CreatedAtUtc < MinimumInterval))
        {
            return UserErrors.CodeRequestedTooRecently;
        }

        foreach (OneTimeToken token in active)
        {
            token.Invalidate(now);
        }

        tokens.Add(OneTimeToken.Issue(userId, purpose, target, hasher, now).Token);
        return Result.Success();
    }
}
