using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Herensuge.Common.Presentation.Security;

/// <summary>Routes of the login SPA served by Herensuge.Api.</summary>
public static class LoginPages
{
    public const string Login = "/login";
    public const string Mfa = "/mfa";
    public const string MfaSetup = "/mfa/setup";
    public const string SelectTenant = "/select-tenant";
    public const string Onboarding = "/onboarding";
    public const string Error = "/error";

    public static string With(string page, string? returnUrl, string? error = null)
    {
        var query = new List<KeyValuePair<string, string?>>();
        if (!string.IsNullOrEmpty(returnUrl))
        {
            query.Add(new("returnUrl", returnUrl));
        }

        if (!string.IsNullOrEmpty(error))
        {
            query.Add(new("error", error));
        }

        return page + QueryString.Create(query).ToUriComponent();
    }
}

public static class ReturnUrls
{
    /// <summary>
    /// Local paths are always allowed. Absolute URLs only when their origin is listed in
    /// Herensuge:AllowedReturnOrigins (for example the portal). Prevents open redirects.
    /// </summary>
    public static bool IsAllowed(string? returnUrl, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        string origin = uri.GetLeftPart(UriPartial.Authority);
        string[] allowed = configuration.GetSection("Herensuge:AllowedReturnOrigins").Get<string[]>() ?? [];
        return allowed.Any(candidate => string.Equals(candidate.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}

public static class RateLimitPolicies
{
    /// <summary>Endpoints that check credentials or send messages: 10 requests per minute per IP.</summary>
    public const string AuthStrict = "auth-strict";

    /// <summary>Other sign-in flow endpoints: 60 requests per minute per IP.</summary>
    public const string Auth = "auth";
}
