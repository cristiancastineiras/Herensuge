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
    public static IReadOnlyList<string> GetAuthenticationMethods(this ClaimsPrincipal principal) =>
        ReadArray(principal, HerensugeClaimTypes.AuthenticationMethod);

    /// <summary>Unconditional permissions of the token's application (Access).</summary>
    /// <summary>
    /// Who acts on behalf of the user (plan D11, D14): an id in the IdP cookie, act: { sub } in tokens. Null in the user's own
    /// sessions.
    /// </summary>
    public static string? GetActor(this ClaimsPrincipal principal)
    {
        string? act = principal.FindFirst(HerensugeClaimTypes.Actor)?.Value;
        if (string.IsNullOrEmpty(act) || !act.StartsWith('{'))
        {
            return act;
        }

        using var document = System.Text.Json.JsonDocument.Parse(act);
        return document.RootElement.TryGetProperty(HerensugeClaimTypes.Subject, out System.Text.Json.JsonElement sub) ? sub.GetString() : null;
    }

    public static IReadOnlyList<string> GetPermissions(this ClaimsPrincipal principal) =>
        ReadArray(principal, HerensugeClaimTypes.Permissions);

    private static IReadOnlyList<string> ReadArray(ClaimsPrincipal principal, string type)
    {
        var methods = new List<string>();
        foreach (Claim claim in principal.FindAll(type))
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
