using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;

namespace Herensuge.Common.Infrastructure.Messaging;

internal static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly ConcurrentDictionary<string, Type> Types = new(StringComparer.Ordinal);

    public static string GetTypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    public static string Serialize(object message) => JsonSerializer.Serialize(message, message.GetType(), Options);

    public static object Deserialize(string content, Type type) =>
        JsonSerializer.Deserialize(content, type, Options)
        ?? throw new InvalidOperationException($"Message of type {type.Name} deserialized to null.");

    /// <summary>Resolves a stored type name, accepting only domain or integration events.</summary>
    public static Type ResolveType(string typeName) =>
        Types.GetOrAdd(typeName, static name =>
        {
            Type type = Type.GetType(name, throwOnError: true)!;
            bool isMessage = typeof(IDomainEvent).IsAssignableFrom(type) || typeof(IntegrationEvent).IsAssignableFrom(type);
            return isMessage ? type : throw new InvalidOperationException($"{name} is not a domain or integration event.");
        });
}
