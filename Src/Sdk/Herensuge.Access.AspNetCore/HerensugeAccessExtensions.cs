using Herensuge.Access.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Validation.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class HerensugeAccessExtensions
{
    /// <summary>
    /// Validates Herensuge's access tokens for this application (issuer, signature with the issuer's JWKS, audience
    /// "{resourceServer}-api") as the default authentication scheme, and registers <see cref="IHerensugeAccess"/> and
    /// the permission policies (.RequirePermission, [RequirePermission]).
    /// </summary>
    public static IServiceCollection AddHerensugeAccess(this IServiceCollection services, Action<HerensugeAccessOptions> configure)
    {
        var options = new HerensugeAccessOptions();
        configure(options);
        options.Validate();

        services.AddOptions<HerensugeAccessOptions>().Configure(configure);
        var authority = new Uri(options.Authority.EndsWith('/') ? options.Authority : options.Authority + "/", UriKind.Absolute);

        services.AddOpenIddict().AddValidation(validation =>
        {
            validation.SetIssuer(authority);
            validation.AddAudiences(options.Audience);
            validation.UseSystemNetHttp();
            validation.UseAspNetCore();
        });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, StepUpResultHandler>();

        services.AddHttpClient(HerensugeAccessClient.HttpClientName, client => client.BaseAddress = authority);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HerensugeAccessClient>();
        services.AddSingleton<IHerensugeAccess>(provider => provider.GetRequiredService<HerensugeAccessClient>());
        return services;
    }

    /// <summary>Requires an authenticated user with <paramref name="permission"/> ("resource:action") in this application.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new RequirePermissionAttribute(permission));
}
