using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Notifications.Application;
using Herensuge.Modules.Notifications.Application.Abstractions;
using Herensuge.Modules.Notifications.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Herensuge.Modules.Notifications.Infrastructure;

/// <summary>Only the inbox tables: Notifications has no state of its own.</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(NotificationsModule.Schema);
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: false);
    }
}

public static class NotificationsModule
{
    public const string Schema = "notifications";

    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<NotificationOptions>().Bind(configuration.GetSection(NotificationOptions.SectionName));
        services.AddOptions<EmailProviderOptions>().Bind(configuration.GetSection(EmailProviderOptions.SectionName));
        services.AddOptions<SmsProviderOptions>().Bind(configuration.GetSection(SmsProviderOptions.SectionName));

        EmailProviderOptions email = configuration.GetSection(EmailProviderOptions.SectionName).Get<EmailProviderOptions>() ?? new();
        SmsProviderOptions sms = configuration.GetSection(SmsProviderOptions.SectionName).Get<SmsProviderOptions>() ?? new();

        switch (email.Provider)
        {
            case NotificationProviders.Log:
                EnsureNotProduction(environment, "Notifications:Email:Provider");
                services.AddSingleton<LogEmailSender>();
                services.AddSingleton<IEmailSender>(provider => new MeteredEmailSender(provider.GetRequiredService<LogEmailSender>(), email.Provider));
                break;
            default:
                throw new InvalidOperationException($"Unknown email provider '{email.Provider}'. Available: {NotificationProviders.Log}.");
        }

        switch (sms.Provider)
        {
            case NotificationProviders.Log:
                EnsureNotProduction(environment, "Notifications:Sms:Provider");
                services.AddSingleton<LogSmsSender>();
                services.AddSingleton<ISmsSender>(provider => new MeteredSmsSender(provider.GetRequiredService<LogSmsSender>(), sms.Provider));
                break;
            default:
                throw new InvalidOperationException($"Unknown SMS provider '{sms.Provider}'. Available: {NotificationProviders.Log}.");
        }

        services.AddModuleDbContext<NotificationsDbContext>(Schema);
        services.AddModuleMessaging(new MessagingModule("notifications", Schema, hasOutbox: false, AssemblyReference.Assembly));
        return services;
    }

    private static void EnsureNotProduction(IHostEnvironment environment, string setting)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                $"The Log provider writes links and codes to the logs and cannot run in Production. Configure a real provider in {setting}.");
        }
    }
}
