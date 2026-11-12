using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Application.Messaging;
using Herensuge.Modules.Audit.Application.Abstractions;

namespace Herensuge.Modules.Audit.Application.EventHandlers;

/// <summary>
/// Subscribes to every integration event of every module. Properties marked [SensitiveData]
/// (links, codes, phone numbers) never reach the audit log.
/// </summary>
internal sealed class RecordAuditEntryHandler(IAuditLog auditLog, IRequestContext requestContext)
    : IIntegrationEventHandler<IntegrationEvent>
{
    private const string Suffix = "IntegrationEvent";

    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RemoveSensitiveProperties } },
    };

    public Task Handle(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Type type = integrationEvent.GetType();
        string action = type.Name.EndsWith(Suffix, StringComparison.Ordinal) ? type.Name[..^Suffix.Length] : type.Name;

        var record = new AuditRecord(
            integrationEvent.Id,
            integrationEvent.OccurredOnUtc,
            action,
            (integrationEvent as ITenantIntegrationEvent)?.TenantId,
            type.GetProperty("UserId")?.GetValue(integrationEvent) as Guid?,
            requestContext.ActorUserId,
            requestContext.IpAddress,
            requestContext.UserAgent,
            JsonSerializer.Serialize(integrationEvent, type, MetadataOptions));

        return auditLog.RecordAsync(record, cancellationToken);
    }

    private static void RemoveSensitiveProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            if (typeInfo.Properties[i].AttributeProvider?.IsDefined(typeof(SensitiveDataAttribute), inherit: true) == true)
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    }
}
