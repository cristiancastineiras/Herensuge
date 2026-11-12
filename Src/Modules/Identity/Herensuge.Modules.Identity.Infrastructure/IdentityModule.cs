using Fido2NetLib;
using Herensuge.Common.Infrastructure;
using Herensuge.Common.Infrastructure.Database;
using Herensuge.Common.Infrastructure.Messaging;
using Herensuge.Modules.Identity.Application;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Application.Services;
using Herensuge.Modules.Identity.Contracts;
using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;
using Herensuge.Modules.Identity.Infrastructure.Authentication;
using Herensuge.Modules.Identity.Infrastructure.Database;
using Herensuge.Modules.Identity.Infrastructure.Jobs;
using Herensuge.Modules.Identity.Infrastructure.PublicApi;
using Herensuge.Modules.Identity.Infrastructure.Repositories;
using Herensuge.Modules.Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Herensuge.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public const string Schema = "identity";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityModuleOptions>().Bind(configuration.GetSection(IdentityModuleOptions.SectionName));
        services.AddOptions<ExternalProvidersOptions>().Bind(configuration.GetSection(ExternalProvidersOptions.SectionName));
        services.AddOptions<Argon2Options>().Bind(configuration.GetSection(Argon2Options.SectionName))
            .Validate(options => options.MemorySizeKiB >= 8192 && options.Iterations >= 1 && options.Parallelism >= 1, "Argon2 parameters are too weak.")
            .ValidateOnStart();
        services.AddOptions<TotpOptions>().Bind(configuration.GetSection(TotpOptions.SectionName));
        services.AddOptions<PasskeyOptions>().Bind(configuration.GetSection(PasskeyOptions.SectionName))
            .Validate(options => options.Origins.Length > 0 && !string.IsNullOrWhiteSpace(options.RelyingPartyId), "Passkey relying party is not configured.")
            .ValidateOnStart();

        services.AddModuleDbContext<IdentityDbContext>(Schema);
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IOneTimeTokenRepository, OneTimeTokenRepository>();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IFido2>(provider =>
        {
            PasskeyOptions passkeys = provider.GetRequiredService<IOptions<PasskeyOptions>>().Value;
            return new Fido2(
                new Fido2Configuration
                {
                    RPID = passkeys.RelyingPartyId,
                    RPName = passkeys.RelyingPartyName,
                    Origins = new HashSet<string>(passkeys.Origins, StringComparer.Ordinal),
                    TimestampDriftTolerance = 300_000,
                },
                null);
        });
        services.AddScoped<IPasskeyService, PasskeyService>();

        services.AddScoped<SignInCompleter>();
        services.AddScoped<OneTimeTokenIssuer>();
        services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();

        services.AddIdentityAuthentication(configuration);
        services.AddHostedService<IdentityCleanupJob>();

        services.AddModuleMessaging(new MessagingModule("identity", Schema, hasOutbox: true, AssemblyReference.Assembly));
        return services;
    }
}
