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

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Herensuge.Bff.Tickets.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string key = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        ticket.Properties.Items[SessionKeyItem] = key;
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        cache.SetAsync(
            Prefix + key,
            _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow() + options.Value.SessionLifetime,
            });

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
