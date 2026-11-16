using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Access.Application;
using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Infrastructure.Database;
using Herensuge.Modules.Access.Infrastructure.PublicApi;
using Herensuge.Modules.Access.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<AccessResolver>();
        services.AddScoped<TenantAdministration>();
        services.AddScoped<IAccessModuleApi, AccessModuleApi>();

        services.AddModuleMessaging(new MessagingModule("access", Schema, hasOutbox: true, AssemblyReference.Assembly));
        return services;
    }
}
