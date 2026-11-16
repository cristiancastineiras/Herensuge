using System.Security.Claims;
using System.Text.Json;

namespace Herensuge.Access.AspNetCore;

/// <summary>Claims of the access tokens Herensuge issues for an application.</summary>
public static class HerensugeClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string SessionId = "sid";
    public const string AuthenticationMethods = "amr";
    public const string AuthenticationTime = "auth_time";
    public const string ClientId = "client_id";
    public const string Roles = "roles";

    /// <summary>Features of the application in the organization's plan.</summary>
    public const string Entitlements = "entitlements";

    /// <summary>Granted without a condition: the SDK allows them without asking.</summary>
    public const string Permissions = "permissions";

    /// <summary>Granted with a condition (MFA, recent sign-in): the SDK asks the decision point.</summary>
    public const string ConditionalPermissions = "conditional_permissions";

    /// <summary>Who acts on behalf of the user (support session, token exchange): { "sub": … }.</summary>
    public const string Actor = "act";

    /// <summary>Values of a claim that may arrive as several claims or as one JSON array.</summary>
    public static IReadOnlyList<string> Values(ClaimsPrincipal user, string type)
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

        return values;
    }
}
