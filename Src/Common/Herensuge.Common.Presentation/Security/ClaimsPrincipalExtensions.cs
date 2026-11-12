using System.Security.Claims;
using System.Text.Json;
using Herensuge.Common.Application.Security;

namespace Herensuge.Common.Presentation.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(HerensugeClaimTypes.Subject), out Guid id)
            ? id
            : throw new InvalidOperationException("The principal has no valid subject claim.");

    public static Guid? GetSessionId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(HerensugeClaimTypes.SessionId), out Guid id) ? id : null;

    public static Guid? GetTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(HerensugeClaimTypes.TenantId), out Guid id) ? id : null;

    public static DateTimeOffset? GetAuthenticationTime(this ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(HerensugeClaimTypes.AuthenticationTime), out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>Reads amr whether it arrives as several claims or as one JSON array claim.</summary>
    public static IReadOnlyList<string> GetAuthenticationMethods(this ClaimsPrincipal principal)
    {
        var methods = new List<string>();
        foreach (Claim claim in principal.FindAll(HerensugeClaimTypes.AuthenticationMethod))
        {
            if (claim.Value.StartsWith('['))
            {
                methods.AddRange(JsonSerializer.Deserialize<string[]>(claim.Value) ?? []);
            }
            else
            {
                methods.Add(claim.Value);
            }
        }

        return [.. methods.Distinct(StringComparer.Ordinal)];
    }
}
