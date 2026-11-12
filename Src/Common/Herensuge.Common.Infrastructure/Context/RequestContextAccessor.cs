using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Application.Security;
using Microsoft.AspNetCore.Http;

namespace Herensuge.Common.Infrastructure.Context;

internal sealed record MessageContext(Guid? ActorUserId, string? IpAddress, string? UserAgent, string? CorrelationId);

/// <summary>Scoped <see cref="IRequestContext"/>: reads the HTTP request, or the message envelope inside the jobs.</summary>
public sealed class RequestContextAccessor(IHttpContextAccessor httpContextAccessor) : IRequestContext
{
    private const int MaxUserAgentLength = 512;

    private MessageContext? _messageContext;

    public Guid? ActorUserId => _messageContext is not null
        ? _messageContext.ActorUserId
        : Guid.TryParse(HttpContext?.User.FindFirstValue(HerensugeClaimTypes.Subject), out Guid id) ? id : null;

    public string? IpAddress => _messageContext is not null ? _messageContext.IpAddress : ReadIpAddress(HttpContext);

    public string? UserAgent => _messageContext is not null ? _messageContext.UserAgent : ReadUserAgent(HttpContext);

    public string? DeviceId => _messageContext is not null
        ? null
        : HttpContext?.Items[RequestContextKeys.DeviceId] as string ?? HttpContext?.Request.Cookies[HerensugeCookies.Device];

    public string? CorrelationId => _messageContext is not null
        ? _messageContext.CorrelationId
        : Activity.Current?.TraceId.ToString() ?? HttpContext?.TraceIdentifier;

    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    internal void UseMessageContext(MessageContext context) => _messageContext = context;

    private static string? ReadIpAddress(HttpContext? httpContext)
    {
        IPAddress? address = httpContext?.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static string? ReadUserAgent(HttpContext? httpContext)
    {
        string? userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            return null;
        }

        return userAgent.Length <= MaxUserAgentLength ? userAgent : userAgent[..MaxUserAgentLength];
    }
}
