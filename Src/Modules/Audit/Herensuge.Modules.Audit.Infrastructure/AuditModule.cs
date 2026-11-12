using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Audit.Application;
using Herensuge.Modules.Audit.Application.Abstractions;
using Herensuge.Modules.Audit.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Modules.Audit.Infrastructure;

public static class AuditModule
{
    public const string Schema = "audit";

    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuditOptions>().Bind(configuration.GetSection(AuditOptions.SectionName));
        services.AddModuleDbContext<AuditDbContext>(Schema);
        services.AddSingleton<IAuditLog, AuditLog>();
        services.AddHostedService<AuditRetentionJob>();

        services.AddModuleMessaging(new MessagingModule("audit", Schema, hasOutbox: false, AssemblyReference.Assembly));
        return services;
    }
}
