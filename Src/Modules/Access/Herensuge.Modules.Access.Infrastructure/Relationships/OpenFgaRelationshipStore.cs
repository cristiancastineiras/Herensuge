using System.Security.Cryptography;
using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;

namespace Herensuge.Modules.Access.Infrastructure.Relationships;

/// <summary>Access:OpenFga. Without ApiUrl, per-object permissions are off (relationships_disabled).</summary>
public sealed class OpenFgaOptions
{
    public const string SectionName = "Access:OpenFga";

    /// <summary>For example http://herensuge-openfga:8080.</summary>
    public string ApiUrl { get; set; } = string.Empty;
}

/// <summary>The OpenFGA client once <see cref="OpenFgaBootstrapper"/> has the store and model ids; null until then.</summary>
internal sealed class OpenFgaConnection
{
    public OpenFgaClient? Client { get; private set; }

    public void Connect(string apiUrl, string storeId, string modelId) =>
        Client = new OpenFgaClient(new ClientConfiguration { ApiUrl = apiUrl, StoreId = storeId, AuthorizationModelId = modelId });
}

/// <summary>
/// Plan D10 with OpenFGA: 2-second timeout and HIGHERCONSISTENCY on reads (a share or a revocation counts at once).
/// Every failure is a deny: relationships_unavailable.
/// </summary>
internal sealed partial class OpenFgaRelationshipStore(OpenFgaConnection connection, ILogger<OpenFgaRelationshipStore> logger) : IRelationshipStore
{
    private const string MemberRelation = "member";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public bool IsEnabled => true;

    public async Task<RelationCheck> CheckAsync(
        RelationshipObject target,
        string relation,
        RelationshipSubject subject,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        string user = RelationshipIds.Subject(subject, target.ResourceServer, target.TenantId);
        CheckResponse? response = await CallAsync(
            (client, token) => client.Check(
                new ClientCheckRequest
                {
                    User = user,
                    Relation = relation,
                    Object = RelationshipIds.Object(target),
                    ContextualTuples = GroupTuples(user, groupIds),
                },
                new ClientCheckOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY },
                token),
            cancellationToken);
        return response is null ? RelationCheck.Unavailable : response.Allowed == true ? RelationCheck.Held : RelationCheck.Missing;
    }

    public async Task<IReadOnlyList<string>?> ListObjectsAsync(
        string resourceServer,
        Guid tenantId,
        string type,
        string relation,
        RelationshipSubject subject,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        string user = RelationshipIds.Subject(subject, resourceServer, tenantId);
        ListObjectsResponse? response = await CallAsync(
            (client, token) => client.ListObjects(
                new ClientListObjectsRequest
                {
                    User = user,
                    Relation = relation,
                    Type = RelationshipIds.ObjectType,
                    ContextualTuples = GroupTuples(user, groupIds),
                },
                new ClientListObjectsOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY },
                token),
            cancellationToken);

        // The store holds every application and organization: keep only this one's objects of this type.
        string prefix = $"{RelationshipIds.ObjectPrefix(resourceServer, tenantId)}{type}/";
        return response is null
            ? null
            : [.. response.Objects.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).Select(id => id[prefix.Length..])];
    }

    public async Task<bool> WriteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken) =>
        await CallAsync(
            (client, token) => client.Write(
                new ClientWriteRequest { Writes = [Tuple(target, relation, subject)] },
                new ClientWriteOptions { Conflict = new ConflictOptions { OnDuplicateWrites = OnDuplicateWrites.Ignore } },
                token),
            cancellationToken) is not null;

    public async Task<bool> DeleteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken)
    {
        ClientTupleKey tuple = Tuple(target, relation, subject);
        return await CallAsync(
            (client, token) => client.Write(
                new ClientWriteRequest { Deletes = [new ClientTupleKeyWithoutCondition { User = tuple.User, Relation = tuple.Relation, Object = tuple.Object }] },
                new ClientWriteOptions { Conflict = new ConflictOptions { OnMissingDeletes = OnMissingDeletes.Ignore } },
                token),
            cancellationToken) is not null;
    }

    public async Task DeleteSubjectAsync(Guid? tenantId, RelationshipSubject subject, CancellationToken cancellationToken)
    {
        OpenFgaClient client = connection.Client ?? throw new InvalidOperationException("The relationship store is not ready yet.");
        string user = RelationshipIds.Subject(subject, string.Empty, Guid.Empty);
        string? tenantSegment = tenantId is { } id ? $"/{id:D}/" : null;
        string? continuation = null;
        do
        {
            ReadResponse page = await client.Read(
                new ClientReadRequest { User = user, Object = $"{RelationshipIds.ObjectType}:" },
                new ClientReadOptions { PageSize = 100, ContinuationToken = continuation },
                cancellationToken);
            List<ClientTupleKeyWithoutCondition> deletes =
            [
                .. page.Tuples
                    .Where(tuple => tenantSegment is null || tuple.Key.Object.Contains(tenantSegment, StringComparison.Ordinal))
                    .Select(tuple => new ClientTupleKeyWithoutCondition { User = tuple.Key.User, Relation = tuple.Key.Relation, Object = tuple.Key.Object }),
            ];
            if (deletes.Count > 0)
            {
                await client.DeleteTuples(deletes, null, cancellationToken);
            }

            continuation = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
        }
        while (continuation is not null);
    }

    /// <summary>The user's current groups (plan D10): never stored, so a group change counts at once.</summary>
    private static List<ClientTupleKey> GroupTuples(string user, IReadOnlyCollection<Guid> groupIds) =>
        [.. groupIds.Select(groupId => new ClientTupleKey { User = user, Relation = MemberRelation, Object = $"group:{groupId:D}" })];

    private static ClientTupleKey Tuple(RelationshipObject target, string relation, RelationshipSubject subject) => new()
    {
        User = RelationshipIds.Subject(subject, target.ResourceServer, target.TenantId),
        Relation = relation,
        Object = RelationshipIds.Object(target),
    };

    /// <summary>Null when the store is not ready, fails or takes longer than 2 seconds.</summary>
    private async Task<T?> CallAsync<T>(Func<OpenFgaClient, CancellationToken, Task<T>> call, CancellationToken cancellationToken)
        where T : class
    {
        if (connection.Client is not { } client)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            return await call(client, timeout.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenFGA did not answer; the decision is a deny (relationships_unavailable)")]
    private partial void LogUnavailable(Exception exception);
}

/// <summary>Per-object permissions are off: every relation check is relationships_disabled.</summary>
internal sealed class NullRelationshipStore : IRelationshipStore
{
    public bool IsEnabled => false;

    public Task<RelationCheck> CheckAsync(RelationshipObject target, string relation, RelationshipSubject subject, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken) =>
        Task.FromResult(RelationCheck.Disabled);

    public Task<IReadOnlyList<string>?> ListObjectsAsync(string resourceServer, Guid tenantId, string type, string relation, RelationshipSubject subject, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>?>(null);

    public Task<bool> WriteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> DeleteAsync(RelationshipObject target, string relation, RelationshipSubject subject, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task DeleteSubjectAsync(Guid? tenantId, RelationshipSubject subject, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Store and model ids of OpenFGA (one row): created once, the model rewritten when model.json changes.</summary>
public sealed class RelationshipStoreSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string StoreId { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string ModelHash { get; set; } = string.Empty;
}

/// <summary>
/// Creates the OpenFGA store and writes the fixed model (plan D10), retrying every 10 seconds until OpenFGA answers;
/// until then checks are relationships_unavailable (fail closed).
/// </summary>
internal sealed partial class OpenFgaBootstrapper(
    IServiceScopeFactory scopeFactory,
    OpenFgaConnection connection,
    Microsoft.Extensions.Options.IOptions<OpenFgaOptions> options,
    ILogger<OpenFgaBootstrapper> logger) : BackgroundService
{
    internal static readonly string Model = LoadModel();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiUrl))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BootstrapAsync(stoppingToken);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogRetry(exception);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        string apiUrl = options.Value.ApiUrl;
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        AccessDbContext db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        string hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Model)));

        RelationshipStoreSettings? settings = await db.RelationshipStores.SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            CreateStoreResponse store = await new OpenFgaClient(new ClientConfiguration { ApiUrl = apiUrl })
                .CreateStore(new ClientCreateStoreRequest { Name = "herensuge" }, null, cancellationToken);
            settings = new RelationshipStoreSettings { StoreId = store.Id };
            db.RelationshipStores.Add(settings);
        }

        if (settings.ModelHash != hash)
        {
            WriteAuthorizationModelResponse model = await new OpenFgaClient(new ClientConfiguration { ApiUrl = apiUrl, StoreId = settings.StoreId })
                .WriteAuthorizationModel(ClientWriteAuthorizationModelRequest.FromJson(Model), null, cancellationToken);
            settings.ModelId = model.AuthorizationModelId;
            settings.ModelHash = hash;
        }

        // Two instances at once: the second insert fails on the key and the retry reads the first one's ids.
        await db.SaveChangesAsync(cancellationToken);
        connection.Connect(apiUrl, settings.StoreId, settings.ModelId);
        LogReady(settings.StoreId, settings.ModelId);
    }

    private static string LoadModel()
    {
        using Stream stream = typeof(OpenFgaBootstrapper).Assembly.GetManifestResourceStream("Herensuge.Modules.Access.Infrastructure.Relationships.model.json")
            ?? throw new InvalidOperationException("The embedded OpenFGA model is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenFGA is not ready; retrying in 10 seconds")]
    private partial void LogRetry(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenFGA store {StoreId} with model {ModelId}")]
    private partial void LogReady(string storeId, string modelId);
}
