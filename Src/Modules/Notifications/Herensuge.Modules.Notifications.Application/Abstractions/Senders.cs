namespace Herensuge.Modules.Notifications.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string TextBody);

public sealed record SmsMessage(string To, string Text);

/// <summary>Port: every email provider (Log today; Resend, SES, Mailgun… tomorrow) implements it.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Port: every SMS provider (Log today; Twilio, Bird, Vonage… tomorrow) implements it.</summary>
public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Public URL of the login SPA, used to build links (https://id.example.com).</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>Product name shown in emails and SMS.</summary>
    public string ProductName { get; set; } = "Herensuge";

    internal string Link(string path) => $"{PublicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
