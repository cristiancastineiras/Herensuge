using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Tenancy.Application;
using Herensuge.Modules.Tenancy.Application.Abstractions;
using Herensuge.Modules.Tenancy.Application.Services;
using Herensuge.Modules.Tenancy.Contracts;
using Herensuge.Modules.Tenancy.Domain.Invitations;
using Herensuge.Modules.Tenancy.Domain.Memberships;
using Herensuge.Modules.Tenancy.Domain.Tenants;
using Herensuge.Modules.Tenancy.Infrastructure.Database;
using Herensuge.Modules.Tenancy.Infrastructure.PublicApi;
using Herensuge.Modules.Tenancy.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Modules.Tenancy.Infrastructure;

public static class TenancyModule
{
    public const string Schema = "tenancy";

    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<TenancyDbContext>(Schema);
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TenancyDbContext>());
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<TenantAccess>();
        services.AddScoped<ITenancyModuleApi, TenancyModuleApi>();

        services.AddModuleMessaging(new MessagingModule("tenancy", Schema, hasOutbox: true, AssemblyReference.Assembly));
        return services;
    }
}
