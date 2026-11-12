using System.Diagnostics.Metrics;
using Herensuge.Modules.Notifications.Application.Abstractions;

namespace Herensuge.Modules.Notifications.Infrastructure.Providers;

/// <summary>herensuge.notifications.sent{channel, provider, result} around whichever provider is configured.</summary>
internal static class NotificationMetrics
{
    private static readonly Meter Meter = new("Herensuge.Notifications");

    private static readonly Counter<long> Sent = Meter.CreateCounter<long>(
        "herensuge.notifications.sent",
        description: "Emails and SMS handed to the provider, by channel, provider and result.");

    public static async Task TrackAsync(string channel, string provider, Func<Task> send)
    {
        try
        {
            await send();
            Record(channel, provider, "success");
        }
        catch
        {
            Record(channel, provider, "failure");
            throw;
        }
    }

    private static void Record(string channel, string provider, string result) =>
        Sent.Add(
            1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", result));
}

internal sealed class MeteredEmailSender(IEmailSender inner, string provider) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        NotificationMetrics.TrackAsync("email", provider, () => inner.SendAsync(message, cancellationToken));
}

internal sealed class MeteredSmsSender(ISmsSender inner, string provider) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken) =>
        NotificationMetrics.TrackAsync("sms", provider, () => inner.SendAsync(message, cancellationToken));
}
