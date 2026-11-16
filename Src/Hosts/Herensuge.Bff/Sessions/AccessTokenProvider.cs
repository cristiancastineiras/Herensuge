using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace Herensuge.Bff.Sessions;

/// <summary>
/// Returns a valid access token for the current BFF session, refreshing it (with rotation) when it expires within a
/// minute. Each refresh also brings fresh access claims (userinfo, ADR-A13) into the session. A rejected refresh ends
/// the session.
/// </summary>
internal sealed class AccessTokenProvider(
    ITicketStore ticketStore,
    ISessionLock sessionLock,
    OpenIddictClientService client,
    AccessVersions versions,
    TimeProvider timeProvider,
    ILogger<AccessTokenProvider> logger)
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    public async Task<string?> GetAccessTokenAsync(HttpContext httpContext)
    {
        AuthenticateResult authenticated = await httpContext.AuthenticateAsync(BffDefaults.CookieScheme);
        if (!authenticated.Succeeded || authenticated.Properties is not { } properties)
        {
            return null;
        }

        if (IsFresh(properties))
        {
            return properties.GetTokenValue(BffDefaults.AccessToken);
        }

        return properties.Items.TryGetValue(DistributedTicketStore.SessionKeyItem, out string? sessionKey) && sessionKey is not null
            ? (await RefreshAsync(sessionKey, accessVersions: null, httpContext.RequestAborted))?.Properties.GetTokenValue(BffDefaults.AccessToken)
            : null;
    }

    /// <summary>
    /// Refreshes the session under its lock. With <paramref name="accessVersions"/> (plan D4) it refreshes even if the
    /// token is fresh, unless another request already did it for those versions; without it, only when the token
    /// expires within a minute. Null when the session is gone or the identity provider rejected the refresh.
    /// </summary>
    public async Task<AuthenticationTicket?> RefreshAsync(string sessionKey, string? accessVersions, CancellationToken cancellationToken)
    {
        await using IAsyncDisposable? acquired = await sessionLock.AcquireAsync(sessionKey, TimeSpan.FromSeconds(10), cancellationToken);
        if (acquired is null)
        {
            return null;
        }

        // Another request may have refreshed while this one waited for the lock.
        AuthenticationTicket? ticket = await ticketStore.RetrieveAsync(sessionKey);
        if (ticket is null)
        {
            return null;
        }

        bool upToDate = accessVersions is null
            ? IsFresh(ticket.Properties)
            : ticket.Properties.Items.TryGetValue(AccessVersions.SessionItem, out string? saved) && saved == accessVersions;
        if (upToDate)
        {
            return ticket;
        }

        string? refreshToken = ticket.Properties.GetTokenValue(BffDefaults.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        try
        {
            OpenIddictClientModels.RefreshTokenAuthenticationResult result = await client.AuthenticateWithRefreshTokenAsync(new()
            {
                RefreshToken = refreshToken,
                CancellationToken = cancellationToken,
            });

            AuthenticationProperties properties = ticket.Properties;
            properties.UpdateTokenValue(BffDefaults.AccessToken, result.AccessToken);
            properties.UpdateTokenValue(
                BffDefaults.AccessTokenExpiresAt,
                (result.AccessTokenExpirationDate ?? timeProvider.GetUtcNow().AddMinutes(5)).ToString("O", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                properties.UpdateTokenValue(BffDefaults.RefreshToken, result.RefreshToken);
            }

            if (!string.IsNullOrEmpty(result.IdentityToken))
            {
                properties.UpdateTokenValue(BffDefaults.IdentityToken, result.IdentityToken);
            }

            ClaimsPrincipal principal = BffClaims.Merge(ticket.Principal, result.Principal);
            properties.Items[AccessVersions.SessionItem] =
                accessVersions ?? await versions.ReadAsync(principal.FindFirstValue("tenant_id"), principal.FindFirstValue(OpenIddictConstants.Claims.Subject));

            var refreshed = new AuthenticationTicket(principal, properties, ticket.AuthenticationScheme);
            await ticketStore.RenewAsync(sessionKey, refreshed);
            return refreshed;
        }
        catch (OpenIddictExceptions.ProtocolException exception)
        {
            logger.LogInformation("Refresh rejected by the identity provider ({Error}); the session ends", exception.Error);
            await ticketStore.RemoveAsync(sessionKey);
            return null;
        }
    }

    private bool IsFresh(AuthenticationProperties properties) =>
        DateTimeOffset.TryParse(
            properties.GetTokenValue(BffDefaults.AccessTokenExpiresAt),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset expiresAt)
        && expiresAt - timeProvider.GetUtcNow() > RefreshMargin;
}
