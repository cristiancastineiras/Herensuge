using System.Security.Claims;
using System.Text.Json;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Domain;
using Herensuge.Common.Presentation.Endpoints;
using Herensuge.Common.Presentation.Security;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Oidc.Application;
using Herensuge.Modules.Oidc.Application.Authorization;
using Herensuge.Modules.Oidc.Application.Authorization.Authorize;
using Herensuge.Modules.Oidc.Application.Authorization.EndSession;
using Herensuge.Modules.Oidc.Application.Authorization.GetUserInfo;
using Herensuge.Modules.Oidc.Application.Authorization.ValidateTokenRequest;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Herensuge.Modules.Oidc.Presentation.Connect;

/// <summary>
/// OpenID Connect endpoints in passthrough mode: OpenIddict validates the protocol, these handlers
/// decide who gets which claims.
/// </summary>
internal sealed class ConnectEndpoints : IEndpoint
{
    private const string TenantParameter = "tenant";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).ExcludeFromDescription();
        app.MapPost("/connect/token", ExchangeAsync).ExcludeFromDescription();
        app.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync).ExcludeFromDescription();
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], EndSessionAsync).ExcludeFromDescription();
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext httpContext,
        IQueryHandler<AuthorizeQuery, AuthorizationDecision> handler,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictScopeManager scopes,
        IAccessModuleApi access,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        OpenIddictRequest request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        string returnUrl = BuildReturnUrl(httpContext, request);
        bool silent = request.HasPromptValue(PromptValues.None);
        DateTimeOffset now = timeProvider.GetUtcNow();

        ClaimsPrincipal? session = await IdentityProviderSession.AuthenticateAsync(httpContext);
        bool reauthenticate = session is not null
            && (request.HasPromptValue(PromptValues.Login)
                || (request.MaxAge is { } maxAge
                    && (session.GetAuthenticationTime() is not { } authenticatedAt || now - authenticatedAt > TimeSpan.FromSeconds(maxAge))));

        if (session is null || reauthenticate)
        {
            return silent
                ? Forbid(Errors.LoginRequired, "The user is not signed in.")
                : Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl));
        }

        Guid sessionId = session.GetSessionId() ?? Guid.Empty;
        Guid? requestedTenant = Guid.TryParse(request.GetParameter(TenantParameter)?.ToString(), out Guid parsed) ? parsed : null;
        Result<AuthorizationDecision> result = await handler.Handle(
            new AuthorizeQuery(session.GetUserId(), sessionId, session.GetAuthenticationMethods(), requestedTenant, session.GetTenantId()),
            cancellationToken);
        AuthorizationDecision decision = result.Value;

        switch (decision.Outcome)
        {
            case AuthorizationOutcome.SessionInvalid:
                await IdentityProviderSession.SignOutAsync(httpContext);
                return silent ? Forbid(Errors.LoginRequired, "The session has expired.") : Results.Redirect(LoginPages.With(LoginPages.Login, returnUrl));
            case AuthorizationOutcome.SelectTenant:
                return silent ? Forbid(Errors.InteractionRequired, "An organization must be chosen.") : Results.Redirect(LoginPages.With(LoginPages.SelectTenant, returnUrl));
            case AuthorizationOutcome.Onboarding:
                return silent ? Forbid(Errors.InteractionRequired, "The user has no organization yet.") : Results.Redirect(LoginPages.With(LoginPages.Onboarding, returnUrl));
            case AuthorizationOutcome.TenantForbidden:
                return Forbid(Errors.AccessDenied, "The user cannot access that organization.");
            case AuthorizationOutcome.MfaRequired:
                return silent ? Forbid(Errors.InteractionRequired, "Multi-factor authentication is required.") : Results.Redirect(LoginPages.With(LoginPages.Mfa, returnUrl));
            case AuthorizationOutcome.MfaSetupRequired:
                return silent ? Forbid(Errors.InteractionRequired, "Multi-factor authentication must be set up.") : Results.Redirect(LoginPages.With(LoginPages.MfaSetup, returnUrl));
        }

        AuthorizedUser user = decision.User!;
        if (session.GetTenantId() != user.TenantId)
        {
            await IdentityProviderSession.SetActiveTenantAsync(httpContext, session, user.TenantId);
        }

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.UserId.ToString())
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.EmailVerified, user.EmailVerified)
            .SetClaim(HerensugeClaimTypes.SessionId, sessionId.ToString())
            .SetClaim(HerensugeClaimTypes.TenantId, user.TenantId.ToString())
            .SetClaim(HerensugeClaimTypes.TenantRole, user.TenantRole)
            .SetClaim(Claims.AuthenticationTime, (session.GetAuthenticationTime() ?? now).ToUnixTimeSeconds());
        identity.SetClaims(Claims.AuthenticationMethodReference, [.. session.GetAuthenticationMethods()]);
        if (user.IsPlatformAdmin)
        {
            identity.SetClaims(Claims.Role, [PlatformRoles.PlatformAdmin]);
        }

        identity.SetScopes(request.GetScopes());
        var resources = new List<string>();
        await foreach (string resource in scopes.ListResourcesAsync(identity.GetScopes(), cancellationToken))
        {
            resources.Add(resource);
        }

        identity.SetResources(resources);
        await SetAccessClaimsAsync(identity, access, user.UserId, user.TenantId, cancellationToken);
        identity.SetDestinations(GetDestinations);

        // An explicit authorization ties the tokens to the SSO session so both can be revoked together.
        object application = await applications.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new InvalidOperationException("The client application cannot be found.");
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = await applications.GetIdAsync(application, cancellationToken),
            CreationDate = now,
            Principal = new ClaimsPrincipal(identity),
            Status = Statuses.Valid,
            Subject = user.UserId.ToString(),
            Type = AuthorizationTypes.AdHoc,
        };
        descriptor.Scopes.UnionWith(identity.GetScopes());
        descriptor.Properties[OidcPropertyNames.SessionId] = JsonSerializer.SerializeToElement(sessionId.ToString());
        descriptor.Properties[OidcPropertyNames.TenantId] = JsonSerializer.SerializeToElement(user.TenantId.ToString());

        object authorization = await authorizations.CreateAsync(descriptor, cancellationToken);
        identity.SetAuthorizationId(await authorizations.GetIdAsync(authorization, cancellationToken));

        return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext httpContext,
        IQueryHandler<ValidateTokenRequestQuery, AuthorizedUser> handler,
        IAccessModuleApi access,
        IOpenIddictScopeManager scopes,
        CancellationToken cancellationToken)
    {
        OpenIddictRequest request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            return await IssueMachineTokenAsync(request, scopes, cancellationToken);
        }

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Forbid(Errors.UnsupportedGrantType, "The grant type is not supported.");
        }

        AuthenticateResult authenticated = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (authenticated.Principal is not { } principal
            || !Guid.TryParse(principal.GetClaim(Claims.Subject), out Guid userId)
            || !Guid.TryParse(principal.GetClaim(HerensugeClaimTypes.SessionId), out Guid sessionId)
            || !Guid.TryParse(principal.GetClaim(HerensugeClaimTypes.TenantId), out Guid tenantId))
        {
            return Forbid(Errors.InvalidGrant, "The token is no longer valid.");
        }

        Result<AuthorizedUser> result = await handler.Handle(
            new ValidateTokenRequestQuery(userId, sessionId, tenantId, [.. principal.GetClaims(Claims.AuthenticationMethodReference)]),
            cancellationToken);
        if (result.IsFailure)
        {
            return Forbid(Errors.InvalidGrant, result.Error.Description);
        }

        // Keep the original claims (scopes, authorization, session) and refresh the ones that may change.
        AuthorizedUser user = result.Value;
        var identity = new ClaimsIdentity(principal.Claims, TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.EmailVerified, user.EmailVerified)
            .SetClaim(HerensugeClaimTypes.TenantRole, user.TenantRole);
        identity.SetClaims(Claims.Role, user.IsPlatformAdmin ? [PlatformRoles.PlatformAdmin] : []);
        await SetAccessClaimsAsync(identity, access, userId, tenantId, cancellationToken);
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// client_credentials: the backend of an application, to ask the Access decision point. No user and no session
    /// (sub = client_id), and only the herensuge.access scope. OpenIddict has already checked the secret and permissions.
    /// </summary>
    private static async Task<IResult> IssueMachineTokenAsync(OpenIddictRequest request, IOpenIddictScopeManager scopes, CancellationToken cancellationToken)
    {
        if (request.GetScopes() is not [HerensugeScopes.Access])
        {
            return Forbid(Errors.InvalidScope, $"client_credentials only issues the {HerensugeScopes.Access} scope.");
        }

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, request.ClientId).SetClaim(Claims.ClientId, request.ClientId);
        identity.SetScopes(request.GetScopes());

        var resources = new List<string>();
        await foreach (string resource in scopes.ListResourcesAsync(identity.GetScopes(), cancellationToken))
        {
            resources.Add(resource);
        }

        identity.SetResources(resources);
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// roles and permissions of the application the token is for (RFC 9068, one token one API). Recomputed on every
    /// refresh, so a role change reaches the token within its lifetime (5 minutes).
    /// </summary>
    private static async Task SetAccessClaimsAsync(
        ClaimsIdentity identity,
        IAccessModuleApi access,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        AccessClaims? claims = await access.GetAccessClaimsAsync(userId, tenantId, [.. identity.GetResources()], cancellationToken);
        bool embed = claims?.EmbedPermissions == true;
        identity.SetClaims(HerensugeClaimTypes.Roles, claims is null ? [] : [.. claims.Roles]);
        identity.SetClaims(HerensugeClaimTypes.Permissions, embed ? [.. claims!.Permissions] : []);
        identity.SetClaims(HerensugeClaimTypes.ConditionalPermissions, embed ? [.. claims!.ConditionalPermissions] : []);
    }

    private static async Task<IResult> UserInfoAsync(
        HttpContext httpContext,
        IQueryHandler<GetUserInfoQuery, UserInfoResponse> handler,
        IAccessModuleApi access,
        CancellationToken cancellationToken)
    {
        AuthenticateResult authenticated = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (authenticated.Principal is not { } principal || !Guid.TryParse(principal.GetClaim(Claims.Subject), out Guid userId))
        {
            return Challenge(Errors.InvalidToken, "The access token is not valid.");
        }

        Result<UserInfoResponse> user = await handler.Handle(new GetUserInfoQuery(userId), cancellationToken);
        if (user.IsFailure)
        {
            return Challenge(Errors.InvalidToken, "The user no longer exists.");
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal) { [Claims.Subject] = userId.ToString() };
        if (principal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.Value.DisplayName;
        }

        if (principal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Value.Email;
            claims[Claims.EmailVerified] = user.Value.EmailVerified;
        }

        // Access claims of the token's application, always fresh and whatever embedPermissions says: this is how the
        // BFF learns them (ADR-A13), not from the access token nor from the ID token.
        if (Guid.TryParse(principal.GetClaim(HerensugeClaimTypes.TenantId), out Guid tenantId)
            && await access.GetAccessClaimsAsync(userId, tenantId, [.. principal.GetResources()], cancellationToken) is { } accessClaims)
        {
            claims[HerensugeClaimTypes.Roles] = accessClaims.Roles;
            claims[HerensugeClaimTypes.Permissions] = accessClaims.Permissions;
            claims[HerensugeClaimTypes.ConditionalPermissions] = accessClaims.ConditionalPermissions;
        }

        return Results.Ok(claims);
    }

    private static async Task<IResult> EndSessionAsync(
        HttpContext httpContext,
        ICommandHandler<EndSessionCommand> handler,
        CancellationToken cancellationToken)
    {
        ClaimsPrincipal? session = await IdentityProviderSession.AuthenticateAsync(httpContext);
        if (session?.GetSessionId() is { } sessionId)
        {
            await handler.Handle(new EndSessionCommand(sessionId), cancellationToken);
        }

        await IdentityProviderSession.SignOutAsync(httpContext);

        // OpenIddict redirects to the validated post_logout_redirect_uri, or to "/" when there is none.
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name:
                if (claim.Subject?.HasScope(Scopes.Profile) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email or Claims.EmailVerified:
                if (claim.Subject?.HasScope(Scopes.Email) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Subject or Claims.Role or Claims.AuthenticationMethodReference or Claims.AuthenticationTime
                or HerensugeClaimTypes.SessionId or HerensugeClaimTypes.TenantId or HerensugeClaimTypes.TenantRole:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    /// <summary>The same authorization request without prompt/max_age, so the login SPA can resume it.</summary>
    private static string BuildReturnUrl(HttpContext httpContext, OpenIddictRequest request)
    {
        IEnumerable<KeyValuePair<string, string?>> parameters = request.GetParameters()
            .Where(parameter => parameter.Key is not (Parameters.Prompt or Parameters.MaxAge))
            .Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value.ToString()));

        return httpContext.Request.PathBase + httpContext.Request.Path + QueryString.Create(parameters).ToUriComponent();
    }

    private static IResult Forbid(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static IResult Challenge(string error, string description) => Results.Challenge(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
