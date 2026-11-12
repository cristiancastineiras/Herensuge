using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Dapper;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Herensuge.Common.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Herensuge.Common.Infrastructure.Messaging;

internal enum MessageKind
{
    Outbox,
    Inbox,
}

/// <summary>
/// Polls one module's outbox or inbox. Rows are locked with FOR UPDATE SKIP LOCKED, so several
/// instances of the host can run side by side without processing a message twice. Delivery is
/// at-least-once; every handler that succeeded is recorded so retries skip it.
/// </summary>
internal sealed class MessageProcessor(
    MessagingModule module,
    MessageKind kind,
    NpgsqlDataSource dataSource,
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    TimeProvider timeProvider,
    ILogger<MessageProcessor> logger) : BackgroundService
{
    private const int PendingRefreshEveryTicks = 15;
    private const int PurgeEveryTicks = 600;

    private static readonly ActivitySource ActivitySource = new("Herensuge.Messaging");
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> Invokers = new();

    private static readonly MethodInfo InvokeDomainHandlerMethod =
        typeof(MessageProcessor).GetMethod(nameof(InvokeDomainHandler), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo InvokeIntegrationHandlerMethod =
        typeof(MessageProcessor).GetMethod(nameof(InvokeIntegrationHandler), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly string _kindName = kind == MessageKind.Outbox ? "outbox" : "inbox";
    private readonly string _messages = kind == MessageKind.Outbox ? $"{module.Schema}.outbox_messages" : $"{module.Schema}.inbox_messages";
    private readonly string _consumers = kind == MessageKind.Outbox ? $"{module.Schema}.outbox_message_consumers" : $"{module.Schema}.inbox_message_consumers";
    private readonly string _consumerKey = kind == MessageKind.Outbox ? "outbox_message_id" : "inbox_message_id";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MessagingOptions settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollingInterval, timeProvider);
        long tick = 0;

        do
        {
            try
            {
                int processed;
                do
                {
                    processed = await ProcessBatchAsync(settings, stoppingToken);
                }
                while (processed == settings.BatchSize && !stoppingToken.IsCancellationRequested);

                tick++;
                if (tick % PendingRefreshEveryTicks == 1)
                {
                    await RefreshPendingAsync(stoppingToken);
                }

                if (tick % PurgeEveryTicks == 0)
                {
                    await PurgeAsync(settings, stoppingToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Processing the {Kind} of module {Module} failed", _kindName, module.Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> ProcessBatchAsync(MessagingOptions settings, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        List<MessageRow> messages = (await connection.QueryAsync<MessageRow>(new CommandDefinition(
            $"""
            SELECT id, type, content, attempts, correlation_id, trace_parent, actor_user_id, ip_address, user_agent
            FROM {_messages}
            WHERE processed_on_utc IS NULL AND (next_attempt_on_utc IS NULL OR next_attempt_on_utc <= @Now)
            ORDER BY occurred_on_utc
            LIMIT @BatchSize
            FOR UPDATE SKIP LOCKED
            """,
            new { Now = now, settings.BatchSize },
            transaction,
            cancellationToken: cancellationToken))).AsList();

        foreach (MessageRow message in messages)
        {
            await ProcessMessageAsync(connection, transaction, message, settings, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MessageRow message,
        MessagingOptions settings,
        DateTime now,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ActivitySource.StartActivity(
            $"{_kindName} {module.Name}",
            ActivityKind.Consumer,
            message.TraceParent);
        activity?.SetTag("messaging.message.id", message.Id);
        activity?.SetTag("messaging.message.type", message.Type);

        await transaction.SaveAsync("message", cancellationToken);

        try
        {
            Type messageType = MessageSerializer.ResolveType(message.Type);
            object payload = MessageSerializer.Deserialize(message.Content, messageType);

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<RequestContextAccessor>().UseMessageContext(new MessageContext(
                message.ActorUserId,
                message.IpAddress,
                message.UserAgent,
                message.CorrelationId));

            HashSet<string> completed = [.. await connection.QueryAsync<string>(new CommandDefinition(
                $"SELECT name FROM {_consumers} WHERE {_consumerKey} = @Id",
                new { message.Id },
                transaction,
                cancellationToken: cancellationToken))];

            foreach ((object handler, Func<object, object, CancellationToken, Task> invoke) in ResolveHandlers(scope.ServiceProvider, messageType))
            {
                string name = handler.GetType().FullName!;
                if (completed.Contains(name))
                {
                    continue;
                }

                await invoke(handler, payload, cancellationToken);

                await connection.ExecuteAsync(new CommandDefinition(
                    $"INSERT INTO {_consumers} ({_consumerKey}, name) VALUES (@Id, @Name) ON CONFLICT DO NOTHING",
                    new { message.Id, Name = name },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                $"UPDATE {_messages} SET processed_on_utc = @Now, error = NULL WHERE id = @Id",
                new { Now = now, message.Id },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.ReleaseAsync("message", cancellationToken);
            MessagingMetrics.RecordProcessed(module.Name, _kindName, "success");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync("message", cancellationToken);

            int attempts = message.Attempts + 1;
            bool exhausted = attempts >= settings.MaxAttempts;
            TimeSpan delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempts)));

            logger.LogError(
                exception,
                "{Kind} message {MessageId} ({MessageType}) of module {Module} failed on attempt {Attempt}{Final}",
                _kindName,
                message.Id,
                message.Type,
                module.Name,
                attempts,
                exhausted ? " and will not be retried" : string.Empty);

            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                UPDATE {_messages}
                SET attempts = @Attempts, error = @Error, next_attempt_on_utc = @NextAttempt, processed_on_utc = @Processed
                WHERE id = @Id
                """,
                new
                {
                    Attempts = attempts,
                    Error = exception.ToString(),
                    NextAttempt = now + delay,
                    Processed = exhausted ? now : (DateTime?)null,
                    message.Id,
                },
                transaction,
                cancellationToken: cancellationToken));

            MessagingMetrics.RecordProcessed(module.Name, _kindName, exhausted ? "dead" : "failure");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        }
    }

    private IEnumerable<(object Handler, Func<object, object, CancellationToken, Task> Invoke)> ResolveHandlers(
        IServiceProvider provider,
        Type messageType)
    {
        if (kind == MessageKind.Outbox)
        {
            Type serviceType = typeof(IDomainEventHandler<>).MakeGenericType(messageType);
            Func<object, object, CancellationToken, Task> invoke = GetInvoker(serviceType, InvokeDomainHandlerMethod, messageType);
            return provider.GetServices(serviceType)
                .Where(handler => handler is not null && module.OwnsHandler(handler.GetType()))
                .Select(handler => (handler!, invoke));
        }

        Type specificType = typeof(IIntegrationEventHandler<>).MakeGenericType(messageType);
        Type catchAllType = typeof(IIntegrationEventHandler<IntegrationEvent>);
        Func<object, object, CancellationToken, Task> invokeSpecific = GetInvoker(specificType, InvokeIntegrationHandlerMethod, messageType);
        Func<object, object, CancellationToken, Task> invokeCatchAll = GetInvoker(catchAllType, InvokeIntegrationHandlerMethod, typeof(IntegrationEvent));

        return provider.GetServices(specificType).Select(handler => (handler, invokeSpecific))
            .Concat(provider.GetServices(catchAllType).Select(handler => (handler, invokeCatchAll)))
            .Where(pair => pair.handler is not null && module.OwnsHandler(pair.handler.GetType()))
            .Select(pair => (pair.handler!, pair.Item2));
    }

    private static Func<object, object, CancellationToken, Task> GetInvoker(Type serviceType, MethodInfo method, Type messageType) =>
        Invokers.GetOrAdd(serviceType, _ => method.MakeGenericMethod(messageType).CreateDelegate<Func<object, object, CancellationToken, Task>>());

    private static Task InvokeDomainHandler<TEvent>(object handler, object message, CancellationToken cancellationToken)
        where TEvent : IDomainEvent =>
        ((IDomainEventHandler<TEvent>)handler).Handle((TEvent)message, cancellationToken);

    private static Task InvokeIntegrationHandler<TEvent>(object handler, object message, CancellationToken cancellationToken)
        where TEvent : IntegrationEvent =>
        ((IIntegrationEventHandler<TEvent>)handler).Handle((TEvent)message, cancellationToken);

    private async Task RefreshPendingAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        long pending = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT count(*) FROM {_messages} WHERE processed_on_utc IS NULL",
            cancellationToken: cancellationToken));
        MessagingMetrics.SetPending(module.Name, _kindName, pending);
    }

    private async Task PurgeAsync(MessagingOptions settings, CancellationToken cancellationToken)
    {
        DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.Retention;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            DELETE FROM {_consumers} c USING {_messages} m
            WHERE c.{_consumerKey} = m.id AND m.processed_on_utc < @Cutoff AND m.error IS NULL
            """,
            new { Cutoff = cutoff },
            transaction,
            cancellationToken: cancellationToken));

        int deleted = await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {_messages} WHERE processed_on_utc < @Cutoff AND error IS NULL",
            new { Cutoff = cutoff },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Purged {Count} processed {Kind} messages of module {Module}", deleted, _kindName, module.Name);
        }
    }

    private sealed class MessageRow
    {
        public Guid Id { get; init; }

        public string Type { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public string? CorrelationId { get; init; }

        public string? TraceParent { get; init; }

        public Guid? ActorUserId { get; init; }

        public string? IpAddress { get; init; }

        public string? UserAgent { get; init; }
    }
}

internal static class MessagingMetrics
{
    private static readonly Meter Meter = new("Herensuge.Messaging");

    private static readonly ConcurrentDictionary<(string Module, string Kind), long> Pending = new();

    private static readonly Counter<long> Processed = Meter.CreateCounter<long>(
        "herensuge.messaging.processed",
        description: "Outbox and inbox messages processed, by result (success, failure, dead).");

    private static readonly ObservableGauge<long> PendingGauge = Meter.CreateObservableGauge(
        "herensuge.messaging.pending",
        () => Pending.Select(pair => new Measurement<long>(
            pair.Value,
            new KeyValuePair<string, object?>("module", pair.Key.Module),
            new KeyValuePair<string, object?>("kind", pair.Key.Kind))),
        description: "Messages waiting to be processed.");

    public static void RecordProcessed(string module, string kind, string result) =>
        Processed.Add(
            1,
            new KeyValuePair<string, object?>("module", module),
            new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("result", result));

    public static void SetPending(string module, string kind, long value)
    {
        _ = PendingGauge;
        Pending[(module, kind)] = value;
    }
}
