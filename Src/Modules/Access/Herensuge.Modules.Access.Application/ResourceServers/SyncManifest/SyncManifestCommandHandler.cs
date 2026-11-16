namespace Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;

internal sealed class SyncManifestCommandHandler(
    IResourceServerRepository servers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SyncManifestCommand, ManifestSyncResponse>
{
    public async Task<Result<ManifestSyncResponse>> Handle(SyncManifestCommand command, CancellationToken cancellationToken)
    {
        AccessManifest manifest = command.Manifest;
        ResourceServer? server = await servers.GetByKeyAsync(manifest.ResourceServer ?? string.Empty, cancellationToken);

        if (command.CallerClientId is { } caller && (server is null || server.ClientId != caller || manifest.ClientId != caller))
        {
            return AccessErrors.NotYourResourceServer;
        }

        ResourceServer? owner = string.IsNullOrEmpty(manifest.ClientId) ? null : await servers.GetByClientIdAsync(manifest.ClientId, cancellationToken);
        if (owner is not null && owner.Key != manifest.ResourceServer)
        {
            return AccessErrors.ClientInUse;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (server is null)
        {
            Result<ResourceServer> registered = ResourceServer.Register(manifest, now);
            if (registered.IsFailure)
            {
                return registered.Error;
            }

            server = registered.Value;
            servers.Add(server);
        }
        else
        {
            Result applied = server.ApplyManifest(manifest, now);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ManifestSyncResponse(server.Key, server.ManifestVersion, server.Scope, server.Audience);
    }
}
