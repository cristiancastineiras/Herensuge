using System.Diagnostics.Metrics;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Access.Application.Decisions;

/// <summary>
/// The decision point (PDP), asked by an application's backend about one of its users. Several checks for the
/// same subject in one call (AuthZEN "evaluations").
/// </summary>
public sealed record EvaluateAccessQuery(string CallerClientId, AccessSubject Subject, IReadOnlyList<AccessCheck> Checks)
    : IQuery<IReadOnlyList<AccessDecision>>;

/// <param name="SessionId">sid of the user's access token: a closed session is denied at once.</param>
public sealed record AccessSubject(
    Guid UserId,
    Guid TenantId,
    Guid? SessionId,
    IReadOnlyList<string> AuthenticationMethods,
    DateTimeOffset? AuthenticatedAt);

/// <param name="ResourceServer">Null: the caller's own application.</param>
/// <param name="Permission">"invoice:approve" (AuthZEN resource.type + ":" + action.name).</param>
public sealed record AccessCheck(string? ResourceServer, string Permission);

/// <param name="Reason">granted, unknown_permission, session_inactive, not_member, no_role or step_up_required.</param>
public sealed record AccessDecision(bool Decision, string Reason);

public static class AccessReasons
{
    public const string Granted = "granted";
    public const string UnknownPermission = "unknown_permission";
    public const string SessionInactive = "session_inactive";
    public const string NotMember = "not_member";
    public const string NoRole = "no_role";

    /// <summary>The permission requires MFA or a recent sign-in: the app sends the user to sign in again (max_age=0).</summary>
    public const string StepUpRequired = "step_up_required";
}

internal sealed class EvaluateAccessQueryHandler(
    IResourceServerRepository servers,
    AccessResolver resolver,
    IIdentityModuleApi identity,
    TimeProvider timeProvider) : IQueryHandler<EvaluateAccessQuery, IReadOnlyList<AccessDecision>>
{
    // Same window as the RequireRecentAuthentication endpoint filter.
    private static readonly TimeSpan RecentAuthentication = TimeSpan.FromMinutes(10);

    private static readonly Meter Meter = new("Herensuge.Access");

    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        "herensuge.access.decisions",
        description: "Access decisions by application, result (permit, deny) and reason.");

    public async Task<Result<IReadOnlyList<AccessDecision>>> Handle(EvaluateAccessQuery query, CancellationToken cancellationToken)
    {
        // Every application involved must belong to the caller: an app cannot ask about another app's permissions.
        var applications = new Dictionary<string, ResourceServer>(StringComparer.Ordinal);
        foreach (string? key in query.Checks.Select(check => check.ResourceServer).Distinct())
        {
            ResourceServer? server = key is null
                ? await servers.GetByClientIdAsync(query.CallerClientId, cancellationToken)
                : await servers.GetByKeyAsync(key, cancellationToken);
            if (server is null)
            {
                return AccessErrors.ResourceServerNotFound;
            }

            if (server.ClientId != query.CallerClientId)
            {
                return AccessErrors.NotYourResourceServer;
            }

            applications[key ?? string.Empty] = server;
        }

        AccessSubject subject = query.Subject;
        bool sessionActive = subject.SessionId is { } sessionId
            && await identity.IsSessionActiveAsync(sessionId, subject.UserId, cancellationToken);

        var effective = new Dictionary<Guid, EffectiveAccess?>();
        var decisions = new List<AccessDecision>(query.Checks.Count);
        foreach (AccessCheck check in query.Checks)
        {
            ResourceServer server = applications[check.ResourceServer ?? string.Empty];
            if (sessionActive && !effective.ContainsKey(server.Id))
            {
                effective[server.Id] = await resolver.ResolveAsync(server, subject.TenantId, subject.UserId, cancellationToken);
            }

            AccessDecision decision = Decide(server, check.Permission, sessionActive ? effective[server.Id] : null, sessionActive, subject);
            Decisions.Add(
                1,
                new KeyValuePair<string, object?>("resource_server", server.Key),
                new KeyValuePair<string, object?>("decision", decision.Decision ? "permit" : "deny"),
                new KeyValuePair<string, object?>("reason", decision.Reason));
            decisions.Add(decision);
        }

        return decisions;
    }

    private AccessDecision Decide(ResourceServer server, string permissionKey, EffectiveAccess? access, bool sessionActive, AccessSubject subject)
    {
        Permission? permission = server.ActivePermissions.FirstOrDefault(candidate => candidate.Key == permissionKey);
        if (permission is null)
        {
            return Deny(AccessReasons.UnknownPermission);
        }

        if (!sessionActive)
        {
            return Deny(AccessReasons.SessionInactive);
        }

        if (access is null)
        {
            return Deny(AccessReasons.NotMember);
        }

        if (!access.Permissions.Contains(permissionKey, StringComparer.Ordinal))
        {
            return Deny(AccessReasons.NoRole);
        }

        bool conditionMet = permission.Requires switch
        {
            PermissionRequirements.Mfa => subject.AuthenticationMethods.Contains(AuthenticationMethods.MultiFactor, StringComparer.Ordinal),
            PermissionRequirements.RecentAuthentication => subject.AuthenticatedAt is { } at && timeProvider.GetUtcNow() - at <= RecentAuthentication,
            _ => true,
        };

        return conditionMet ? new AccessDecision(true, AccessReasons.Granted) : Deny(AccessReasons.StepUpRequired);
    }

    private static AccessDecision Deny(string reason) => new(false, reason);
}
