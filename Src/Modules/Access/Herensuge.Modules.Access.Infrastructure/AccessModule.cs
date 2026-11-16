using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Access.Application;
using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Infrastructure.Database;
using Herensuge.Modules.Access.Infrastructure.Notifications;
using Herensuge.Modules.Access.Infrastructure.PublicApi;
using Herensuge.Modules.Access.Infrastructure.Relationships;
using Herensuge.Modules.Access.Infrastructure.Repositories;
using Herensuge.Modules.Access.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Access.Infrastructure;

/// <summary>Permissions of the SaaS applications, organization roles and the decision point (plan: Docs/Arquitectura/Autorizacion.md).</summary>
public static class AccessModule
{
    public const string Schema = "access";

    public static IServiceCollection AddAccessModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<AccessDbContext>(Schema);
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AccessDbContext>());
        services.AddScoped<IResourceServerRepository, ResourceServerRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
        services.AddScoped<AccessEvaluator>();
        services.AddScoped<TenantAdministration>();
        services.AddScoped<IAccessModuleApi, AccessModuleApi>();
        services.AddSingleton<IAccessChangeNotifier, ValkeyAccessChangeNotifier>();
        services.AddHostedService<AccessSeeder>();

        // Per-object permissions (plan D10): OpenFGA when Access:OpenFga:ApiUrl is set; otherwise relationships_disabled.
        services.AddOptions<OpenFgaOptions>().BindConfiguration(OpenFgaOptions.SectionName);
        services.AddSingleton<OpenFgaConnection>();
        services.AddSingleton<IRelationshipStore>(provider =>
            string.IsNullOrWhiteSpace(provider.GetRequiredService<IOptions<OpenFgaOptions>>().Value.ApiUrl)
                ? new NullRelationshipStore()
                : ActivatorUtilities.CreateInstance<OpenFgaRelationshipStore>(provider));
        services.AddHostedService<OpenFgaBootstrapper>();

        services.AddModuleMessaging(new MessagingModule("access", Schema, hasOutbox: true, AssemblyReference.Assembly));
        return services;
    }
}
