using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Access.Application.Decisions;

/// <summary>
/// The decision point (PDP), asked by an application's backend about one of its users. Several checks for the
/// same subject in one call (AuthZEN "evaluations").
/// </summary>
public sealed record EvaluateAccessQuery(string CallerClientId, PdpSubject Subject, IReadOnlyList<AccessCheck> Checks)
    : IQuery<IReadOnlyList<AccessDecision>>;

/// <param name="SessionId">sid of the user's access token: a closed session is denied at once.</param>
/// <param name="Actor">act.sub of the user's token (delegated or support token): conditions are never met.</param>
public sealed record PdpSubject(
    Guid UserId,
    Guid TenantId,
    Guid? SessionId,
    IReadOnlyList<string> AuthenticationMethods,
    DateTimeOffset? AuthenticatedAt,
    string? Actor);

/// <param name="ResourceServer">Null: the caller's own application.</param>
/// <param name="Permission">"invoice:approve" (AuthZEN resource.type + ":" + action.name).</param>
public sealed record AccessCheck(string? ResourceServer, string Permission);

/// <param name="Reason">One of <see cref="AccessReasons"/>.</param>
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

    /// <summary>A condition that a delegated or support token can never meet.</summary>
    public const string ConditionNotMet = "condition_not_met";

    /// <summary>Simulator only: granted by a role, subject to a condition that was not evaluated.</summary>
    public const string GrantedIfCondition = "granted_if_condition";
}

internal sealed class EvaluateAccessQueryHandler(
    IResourceServerRepository servers,
    AccessEvaluator evaluator,
    IIdentityModuleApi identity) : IQueryHandler<EvaluateAccessQuery, IReadOnlyList<AccessDecision>>
{
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

        PdpSubject subject = query.Subject;
        bool sessionActive = subject.SessionId is { } sessionId
            && await identity.IsSessionActiveAsync(sessionId, subject.UserId, cancellationToken);
        var facts = new SubjectFacts(sessionActive, subject.AuthenticationMethods, subject.AuthenticatedAt, subject.Actor);

        var grants = new Dictionary<Guid, AccessGrants?>();
        var decisions = new List<AccessDecision>(query.Checks.Count);
        foreach (AccessCheck check in query.Checks)
        {
            ResourceServer server = applications[check.ResourceServer ?? string.Empty];
            if (sessionActive && !grants.ContainsKey(server.Id))
            {
                grants[server.Id] = await evaluator.ResolveGrantsAsync(server, new AccessSubject(subject.TenantId, subject.UserId), cancellationToken);
            }

            decisions.Add(evaluator.Evaluate(server, grants.GetValueOrDefault(server.Id), facts, check.Permission));
        }

        return decisions;
    }
}
