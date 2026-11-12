using Herensuge.Modules.Notifications.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Notifications.Infrastructure.Providers;

/// <summary>
/// Writes the full email in plain text to the log instead of sending it. Messages contain links and
/// codes, so this provider refuses to run in Production.
/// </summary>
internal sealed class LogEmailSender(IOptions<EmailProviderOptions> options, ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[EMAIL] From: {From} | To: {To} | Subject: {Subject}{NewLine}{Body}",
            options.Value.From,
            message.To,
            message.Subject,
            Environment.NewLine,
            message.TextBody);
        return Task.CompletedTask;
    }
}

/// <summary>Writes the SMS in plain text to the log instead of sending it (not allowed in Production).</summary>
internal sealed class LogSmsSender(IOptions<SmsProviderOptions> options, ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("[SMS] From: {From} | To: {To} | Text: {Text}", options.Value.From, message.To, message.Text);
        return Task.CompletedTask;
    }
}

public sealed class EmailProviderOptions
{
    public const string SectionName = "Notifications:Email";

    /// <summary>Log (development). Real providers are added as new classes, nothing else changes.</summary>
    public string Provider { get; set; } = NotificationProviders.Log;

    public string From { get; set; } = "Herensuge <no-reply@herensuge.local>";
}

public sealed class SmsProviderOptions
{
    public const string SectionName = "Notifications:Sms";

    public string Provider { get; set; } = NotificationProviders.Log;

    public string From { get; set; } = "Herensuge";
}

public static class NotificationProviders
{
    public const string Log = "Log";
}
