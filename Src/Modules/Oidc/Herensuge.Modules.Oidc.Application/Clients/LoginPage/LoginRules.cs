using Herensuge.Common.Application.Security;

namespace Herensuge.Modules.Oidc.Application.Clients.LoginPage;

/// <summary>What /connect/authorize checks against the client's login page before issuing a code.</summary>
public static class LoginRules
{
    /// <summary>
    /// The SSO session was started with a method this client accepts (amr: pwd, pop or fed). Support sessions (imp)
    /// always pass: they only reach the support clients.
    /// </summary>
    public static bool AcceptsSession(LoginMethods methods, IReadOnlyCollection<string> authenticationMethods) =>
        authenticationMethods.Contains(AuthenticationMethods.Impersonation)
        || (methods.Password && authenticationMethods.Contains(AuthenticationMethods.Password))
        || (methods.Passkey && authenticationMethods.Contains(AuthenticationMethods.Passkey))
        || ((methods.Google || methods.Microsoft) && authenticationMethods.Contains(AuthenticationMethods.Federated));

    public static bool IsProfileComplete(LoginSettings settings, IReadOnlyDictionary<string, string> values) =>
        !settings.ProfileFields.Any(field => field.IsMissingIn(values));

    /// <summary>The client's own fields only: one application never sees what another one asked for.</summary>
    public static Dictionary<string, string> ClientValues(LoginSettings settings, IReadOnlyDictionary<string, string> values) =>
        settings.ProfileFields
            .Where(field => values.ContainsKey(field.Key))
            .ToDictionary(field => field.Key, field => values[field.Key], StringComparer.Ordinal);
}
