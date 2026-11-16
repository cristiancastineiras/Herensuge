using System.Security.Claims;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Bff.Sessions;

/// <summary>Claims the BFF keeps in its session: identity (ID token) and access (userinfo, ADR-A13).</summary>
internal static class BffClaims
{
    public const string TenantId = "tenant_id";
    public const string Roles = "roles";
    public const string Permissions = "permissions";
    public const string ConditionalPermissions = "conditional_permissions";
    public const string Entitlements = "entitlements";
    public const string Actor = "act";

    public static readonly string[] Copied =
    [
        Claims.Subject, "sid", Claims.Name, Claims.Email, Claims.EmailVerified, TenantId, "tenant_role",
        Claims.AuthenticationMethodReference, Claims.AuthenticationTime, Claims.Role,
        Roles, Permissions, ConditionalPermissions, Entitlements, Actor,
    ];

    /// <summary>Session identity with the copied claims of <paramref name="source"/>.</summary>
    public static ClaimsPrincipal From(ClaimsPrincipal source)
    {
        var identity = new ClaimsIdentity(BffDefaults.CookieScheme, Claims.Name, Claims.Role);
        identity.AddClaims(source.Claims.Where(claim => Copied.Contains(claim.Type)).Select(claim => new Claim(claim.Type, claim.Value, claim.ValueType)));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// The current session claims with every copied type that <paramref name="refreshed"/> brings replaced; the access
    /// claims are always replaced (an empty userinfo list means "no longer granted").
    /// </summary>
    public static ClaimsPrincipal Merge(ClaimsPrincipal current, ClaimsPrincipal refreshed)
    {
        string[] replaced = [.. Copied.Where(type => refreshed.HasClaim(claim => claim.Type == type) || type is Roles or Permissions or ConditionalPermissions or Entitlements)];
        var identity = new ClaimsIdentity(BffDefaults.CookieScheme, Claims.Name, Claims.Role);
        identity.AddClaims(current.Claims.Where(claim => Copied.Contains(claim.Type) && !replaced.Contains(claim.Type)).Select(Copy));
        identity.AddClaims(refreshed.Claims.Where(claim => replaced.Contains(claim.Type)).Select(Copy));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Values of a claim that may arrive as several claims or as one JSON array.</summary>
    public static List<string> ReadArray(ClaimsPrincipal user, string type)
    {
        var values = new List<string>();
        foreach (Claim claim in user.FindAll(type))
        {
            if (claim.Value.StartsWith('['))
            {
                values.AddRange(JsonSerializer.Deserialize<string[]>(claim.Value) ?? []);
            }
            else
            {
                values.Add(claim.Value);
            }
        }

        return [.. values.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>act.sub of a support session or a delegated token, if any.</summary>
    public static string? ReadActor(ClaimsPrincipal user)
    {
        string? act = user.FindFirstValue(Actor);
        if (string.IsNullOrEmpty(act) || !act.StartsWith('{'))
        {
            return act;
        }

        using JsonDocument document = JsonDocument.Parse(act);
        return document.RootElement.TryGetProperty(Claims.Subject, out JsonElement sub) ? sub.GetString() : null;
    }

    private static Claim Copy(Claim claim) => new(claim.Type, claim.Value, claim.ValueType);
}
