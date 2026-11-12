using System.Reflection;
using Herensuge.Common.Application.Messaging;

namespace Herensuge.Common.Infrastructure.Messaging;

/// <summary>Describes the messaging footprint of one module: its schema, its handlers and what it listens to.</summary>
public sealed class MessagingModule
{
    private readonly HashSet<Type> _handledIntegrationEvents;
    private readonly bool _handlesAllIntegrationEvents;

    public MessagingModule(string name, string schema, bool hasOutbox, params Assembly[] handlerAssemblies)
    {
        // The schema is interpolated into SQL: only lowercase identifiers are accepted.
        if (string.IsNullOrEmpty(schema) || !schema.All(c => c is (>= 'a' and <= 'z') or '_'))
        {
            throw new ArgumentException($"Invalid schema name '{schema}'.", nameof(schema));
        }

        Name = name;
        Schema = schema;
        HasOutbox = hasOutbox;
        HandlerAssemblies = handlerAssemblies;

        Type[] handled =
        [
            .. handlerAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .SelectMany(type => type.GetInterfaces())
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .Select(contract => contract.GetGenericArguments()[0]),
        ];

        _handlesAllIntegrationEvents = handled.Contains(typeof(IntegrationEvent));
        _handledIntegrationEvents = [.. handled];
    }

    public string Name { get; }

    public string Schema { get; }

    public bool HasOutbox { get; }

    public bool HasInbox => _handlesAllIntegrationEvents || _handledIntegrationEvents.Count > 0;

    public IReadOnlyCollection<Assembly> HandlerAssemblies { get; }

    internal bool HandlesIntegrationEvent(Type eventType) =>
        _handlesAllIntegrationEvents || _handledIntegrationEvents.Contains(eventType);

    internal bool OwnsHandler(Type handlerType) => HandlerAssemblies.Contains(handlerType.Assembly);
}

public sealed class MessagingOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 10;

    /// <summary>Processed messages are purged after this period (they may contain links and codes).</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
