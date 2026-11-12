using System.Collections.Immutable;
using System.Text.Json;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Abstractions;
using OpenIddict.Abstractions;

namespace Herensuge.Modules.Oidc.Infrastructure.Tokens;

internal sealed class TokenRevoker(
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens) : ITokenRevoker
{
    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        string session = sessionId.ToString();

        // Materialized first: PostgreSQL cannot run the revocations while the query reader is open.
        var matches = new List<object>();
        await foreach (object authorization in authorizations.FindBySubjectAsync(userId.ToString(), cancellationToken))
        {
            ImmutableDictionary<string, JsonElement> properties = await authorizations.GetPropertiesAsync(authorization, cancellationToken);
            if (properties.TryGetValue(OidcPropertyNames.SessionId, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() == session)
            {
                matches.Add(authorization);
            }
        }

        foreach (object authorization in matches)
        {
            string? id = await authorizations.GetIdAsync(authorization, cancellationToken);
            await authorizations.TryRevokeAsync(authorization, cancellationToken);
            if (id is not null)
            {
                await tokens.RevokeByAuthorizationIdAsync(id, cancellationToken);
            }
        }
    }

    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        string subject = userId.ToString();
        await authorizations.RevokeBySubjectAsync(subject, cancellationToken);
        await tokens.RevokeBySubjectAsync(subject, cancellationToken);
    }
}
