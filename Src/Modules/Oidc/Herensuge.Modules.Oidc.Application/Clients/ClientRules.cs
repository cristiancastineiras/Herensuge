using FluentValidation;
using Herensuge.Common.Application.Security;

namespace Herensuge.Modules.Oidc.Application.Clients;

/// <summary>Validation shared by client creation and update.</summary>
internal static class ClientRules
{
    public static readonly string[] AllowedScopes = ["openid", "profile", "email", "offline_access", HerensugeScopes.Api];

    public static IRuleBuilderOptions<T, string> ValidClientId<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(3, 100)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithMessage("Use lowercase letters, digits and dashes.");

    public static IRuleBuilderOptions<T, IReadOnlyList<string>> ValidRedirectUris<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotNull()
            .Must(uris => uris.Count <= 20)
            .Must(uris => uris.All(IsValidRedirectUri))
            .WithMessage("Redirect URIs must be absolute HTTPS URLs without fragments (http is allowed for localhost only).");

    public static IRuleBuilderOptions<T, IReadOnlyList<string>> ValidScopes<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotNull()
            .Must(scopes => scopes.Contains("openid") && scopes.All(scope => AllowedScopes.Contains(scope)))
            .WithMessage($"Scopes must include openid and be a subset of: {string.Join(", ", AllowedScopes)}.");

    private static bool IsValidRedirectUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}
