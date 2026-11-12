using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace Herensuge.Bff.Sessions;

/// <summary>
/// Returns a valid access token for the current BFF session, refreshing it (with rotation) when it
/// expires within a minute. A rejected refresh ends the session.
/// </summary>
internal sealed class AccessTokenProvider(
    ITicketStore ticketStore,
    ISessionLock sessionLock,
    OpenIddictClientService client,
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

        if (!properties.Items.TryGetValue(DistributedTicketStore.SessionKeyItem, out string? sessionKey) || sessionKey is null)
        {
            return null;
        }

        await using IAsyncDisposable? acquired = await sessionLock.AcquireAsync(sessionKey, TimeSpan.FromSeconds(10), httpContext.RequestAborted);
        if (acquired is null)
        {
            return null;
        }

        // Another request may have refreshed the tokens while this one was waiting for the lock.
        AuthenticationTicket? ticket = await ticketStore.RetrieveAsync(sessionKey);
        if (ticket is null)
        {
            return null;
        }

        if (IsFresh(ticket.Properties))
        {
            return ticket.Properties.GetTokenValue(BffDefaults.AccessToken);
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
                CancellationToken = httpContext.RequestAborted,
            });

            ticket.Properties.UpdateTokenValue(BffDefaults.AccessToken, result.AccessToken);
            ticket.Properties.UpdateTokenValue(
                BffDefaults.AccessTokenExpiresAt,
                (result.AccessTokenExpirationDate ?? timeProvider.GetUtcNow().AddMinutes(5)).ToString("O", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                ticket.Properties.UpdateTokenValue(BffDefaults.RefreshToken, result.RefreshToken);
            }

            await ticketStore.RenewAsync(sessionKey, ticket);
            return result.AccessToken;
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
