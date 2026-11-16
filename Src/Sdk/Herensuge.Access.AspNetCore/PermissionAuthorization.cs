using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Herensuge.Access.AspNetCore;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>[RequirePermission("invoice:read")] on controllers and actions; endpoints use .RequirePermission(…).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Permission)];
}

/// <summary>
/// The SDK rule (plan D3): a permission in the token's "permissions" claim is allowed here; anything else (conditional,
/// on an object, or not in the token) is decided by Herensuge's decision point.
/// </summary>
internal sealed class PermissionHandler(IHerensugeAccess access) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (HerensugeClaims.Values(context.User, HerensugeClaims.Permissions).Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
            return;
        }

        HttpContext? http = context.Resource as HttpContext;
        AccessCheckResult result = await access.CheckAsync(
            context.User,
            requirement.Permission,
            resourceId: null,
            http?.Connection.RemoteIpAddress,
            http?.RequestAborted ?? CancellationToken.None);

        if (result.Allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, result.Reason));
        }
    }
}

/// <summary>
/// A denied step-up answers 401 with the RFC 9470 challenge (error="insufficient_user_authentication"), so the
/// application's BFF or SPA can ask the user to sign in again with MFA. Everything else keeps the default behavior.
/// </summary>
internal sealed class StepUpResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(reason => reason.Message == AccessCheckResult.StepUpRequired) == true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate =
                "Bearer error=\"insufficient_user_authentication\", error_description=\"Multi-factor authentication is required.\", acr_values=\"mfa\"";
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
