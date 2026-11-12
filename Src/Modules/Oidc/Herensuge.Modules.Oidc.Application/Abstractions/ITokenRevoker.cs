namespace Herensuge.Modules.Oidc.Application.Abstractions;

/// <summary>Revokes OIDC authorizations (and their refresh and access tokens).</summary>
public interface ITokenRevoker
{
    /// <summary>Everything issued from one SSO session.</summary>
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Everything issued to the user.</summary>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken);
}
