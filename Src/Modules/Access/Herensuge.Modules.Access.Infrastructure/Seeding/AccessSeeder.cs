using System.Text.Json;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.Modules.Access.Application.ResourceServers.SyncManifest;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Herensuge.Modules.Access.Infrastructure.Seeding;

/// <summary>
/// Herensuge registers its own permissions as one more application (plan D1). Runs at startup after the
/// migrations; an unchanged manifest is a no-op (hash), so every restart and instance can run it.
/// </summary>
internal sealed class AccessSeeder(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AccessManifest manifest = LoadManifest() with
        {
            ClientId = configuration["Oidc:PortalClient:ClientId"] is { Length: > 0 } clientId ? clientId : "herensuge-portal",
        };

        try
        {
            await SyncAsync(manifest, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance registered it at the same time (unique key): read it again and apply.
            await SyncAsync(manifest, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SyncAsync(AccessManifest manifest, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        Result<ManifestSyncResponse> synced = await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<SyncManifestCommand, ManifestSyncResponse>>()
            .Handle(new SyncManifestCommand(manifest, CallerClientId: null, IsSystem: true), cancellationToken);

        // Without its own permissions the portal cannot manage access: do not start half working.
        if (synced.IsFailure)
        {
            throw new InvalidOperationException($"Herensuge's access manifest could not be synced: {synced.Error.Description}");
        }
    }

    private static AccessManifest LoadManifest()
    {
        using Stream stream = typeof(AccessSeeder).Assembly.GetManifestResourceStream("Herensuge.Modules.Access.Infrastructure.Seeding.herensuge.access.json")
            ?? throw new InvalidOperationException("The embedded herensuge.access.json is missing.");
        return JsonSerializer.Deserialize<AccessManifest>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The embedded herensuge.access.json is empty.");
    }
}
