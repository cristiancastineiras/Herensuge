namespace Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;

/// <summary>Registers an application or updates its catalog from its manifest.</summary>
/// <param name="CallerClientId">
/// Null for a platform administrator (may register and change anything). Otherwise the app's own client:
/// it may only update its existing application, and cannot hand it to another client.
/// </param>
public sealed record SyncManifestCommand(AccessManifest Manifest, string? CallerClientId) : ICommand<ManifestSyncResponse>;

public sealed record ManifestSyncResponse(string ResourceServer, int ManifestVersion, string Scope, string Audience);
