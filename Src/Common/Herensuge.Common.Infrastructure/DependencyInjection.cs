using Dapper;
using Herensuge.Common.Application.Context;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Infrastructure.Caching;
using Herensuge.Common.Infrastructure.Context;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Common.Infrastructure.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Herensuge.Common.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommonInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Query handlers map snake_case columns onto PascalCase properties.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<RequestContextAccessor>();
        services.AddScoped<IRequestContext>(provider => provider.GetRequiredService<RequestContextAccessor>());

        services.AddPostgres();
        services.AddScoped<InsertOutboxMessagesInterceptor>();
        services.AddScoped<IEventBus, InMemoryEventBus>();
        services.AddOptions<MessagingOptions>().Bind(configuration.GetSection("Messaging"));

        services.AddValkey(configuration, environment);
        services.AddHerensugeHealthChecks();

        return services;
    }

    /// <summary>Registers the module and starts its outbox and/or inbox processors.</summary>
    public static IServiceCollection AddModuleMessaging(this IServiceCollection services, MessagingModule module)
    {
        services.AddSingleton(module);

        if (module.HasOutbox)
        {
            services.AddSingleton<IHostedService>(provider =>
                ActivatorUtilities.CreateInstance<MessageProcessor>(provider, module, MessageKind.Outbox));
        }

        if (module.HasInbox)
        {
            services.AddSingleton<IHostedService>(provider =>
                ActivatorUtilities.CreateInstance<MessageProcessor>(provider, module, MessageKind.Inbox));
        }

        return services;
    }
}
