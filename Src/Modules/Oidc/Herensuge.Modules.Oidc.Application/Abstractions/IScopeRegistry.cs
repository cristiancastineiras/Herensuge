namespace Herensuge.Modules.Oidc.Application.Abstractions;

/// <summary>OAuth scopes and the API (audience) each one gives access to.</summary>
public interface IScopeRegistry
{
    /// <summary>Creates the scope, or points it to <paramref name="resource"/> if it already exists (idempotent).</summary>
    Task EnsureAsync(string scope, string resource, CancellationToken cancellationToken);
}
