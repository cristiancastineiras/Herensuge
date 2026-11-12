using System.Reflection;
using FluentValidation;
using Herensuge.Common.Application.Behaviors;
using Herensuge.Common.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Common.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every command, query and event handler of the given module assemblies, their
    /// FluentValidation validators and the pipeline: Logging → Validation → handler.
    /// </summary>
    public static IServiceCollection AddCommonApplication(this IServiceCollection services, params Assembly[] moduleAssemblies)
    {
        services.Scan(scan => scan
            .FromAssemblies(moduleAssemblies)
            .AddClasses(
                classes => classes.AssignableToAny(
                    typeof(ICommandHandler<>),
                    typeof(ICommandHandler<,>),
                    typeof(IQueryHandler<,>),
                    typeof(IDomainEventHandler<>),
                    typeof(IIntegrationEventHandler<>)),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);

        // Decorators applied later wrap the earlier ones: Logging ends up outermost.
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandBaseHandler<>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));

        return services;
    }
}
