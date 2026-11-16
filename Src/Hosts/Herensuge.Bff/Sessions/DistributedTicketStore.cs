using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Herensuge.Bff.Sessions;

/// <summary>
/// Server-side sessions: the cookie only carries a random key; the ticket (claims and tokens) lives in
/// Valkey, encrypted with Data Protection.
/// </summary>
internal sealed class DistributedTicketStore(
    IDistributedCache cache,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<BffOptions> options,
    TimeProvider timeProvider) : ITicketStore
{
    public const string SessionKeyItem = ".herensuge.session_key";

    private const string Prefix = "herensuge-bff:session:";
    private const string SidPrefix = "herensuge-bff:sid:";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Herensuge.Bff.Tickets.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string key = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        ticket.Properties.Items[SessionKeyItem] = key;
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var entry = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow() + options.Value.SessionLifetime,
        };
        await cache.SetAsync(Prefix + key, _protector.Protect(TicketSerializer.Default.Serialize(ticket)), entry);

        // sid → key: lets a session closed at the IdP (another device, another app) be dropped at once.
        if (ticket.Principal.FindFirst("sid")?.Value is { } sid)
        {
            await cache.SetStringAsync(SidPrefix + sid, key, entry);
        }
    }

    /// <summary>Drops the BFF session opened from an SSO session that has ended at the IdP.</summary>
    public async Task RemoveBySessionIdAsync(string sid)
    {
        if (await cache.GetStringAsync(SidPrefix + sid) is { } key)
        {
            await cache.RemoveAsync(Prefix + key);
            await cache.RemoveAsync(SidPrefix + sid);
        }
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        byte[]? data = await cache.GetAsync(Prefix + key);
        if (data is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(data));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public Task RemoveAsync(string key) => cache.RemoveAsync(Prefix + key);
}
