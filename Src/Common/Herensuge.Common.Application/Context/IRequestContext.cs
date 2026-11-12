namespace Herensuge.Common.Application.Context;

/// <summary>
/// Who and where a change comes from. Inside HTTP requests it reads the connection; inside the
/// outbox/inbox jobs it carries the envelope of the original request.
/// </summary>
public interface IRequestContext
{
    Guid? ActorUserId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? DeviceId { get; }

    string? CorrelationId { get; }
}

public static class RequestContextKeys
{
    /// <summary>HttpContext.Items key holding the device id issued during the current request.</summary>
    public const string DeviceId = "herensuge.device_id";
}
