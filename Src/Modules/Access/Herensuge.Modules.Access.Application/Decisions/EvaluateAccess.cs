using System.Net;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Access.Application.Decisions;

/// <summary>
/// The decision point (PDP), asked by an application's backend about one of its users or service accounts. Several
/// checks for the same subject in one call (AuthZEN "evaluations").
/// </summary>
public sealed record EvaluateAccessQuery(string CallerClientId, PdpSubject Subject, IReadOnlyList<AccessCheck> Checks)
    : IQuery<IReadOnlyList<AccessDecision>>;

/// <summary>
/// Subject of a PDP request: a user (<see cref="UserId"/>, with the facts of their token) or a service account
/// (<see cref="ClientId"/>, plan D7).
/// </summary>
/// <param name="SessionId">sid of the user's access token: a closed session is denied at once.</param>
/// <param name="Actor">act.sub of the user's token (delegated or support token): conditions are never met.</param>
/// <param name="IpAddress">context.ip of the request, for trusted_network (plan D9).</param>
public sealed record PdpSubject(
    Guid TenantId,
    Guid? UserId,
    string? ClientId,
    Guid? SessionId,
    IReadOnlyList<string> AuthenticationMethods,
    DateTimeOffset? AuthenticatedAt,
    string? Actor,
    IPAddress? IpAddress);

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

    /// <summary>The permission belongs to a feature the organization's plan does not include.</summary>
    public const string NotEntitled = "not_entitled";

    public const string NoRole = "no_role";

    /// <summary>The permission requires MFA or a recent sign-in: the app sends the user to sign in again (max_age=0).</summary>
    public const string StepUpRequired = "step_up_required";

    /// <summary>The permission requires a trusted network and the client IP is not in one (or is private).</summary>
    public const string UntrustedNetwork = "untrusted_network";

    /// <summary>A condition that a delegated or support token, or a service account, can never meet.</summary>
    public const string ConditionNotMet = "condition_not_met";

    /// <summary>Simulator only: granted by a role, subject to a condition that was not evaluated.</summary>
    public const string GrantedIfCondition = "granted_if_condition";
}

internal sealed class EvaluateAccessQueryHandler(
    IResourceServerRepository servers,
    IServiceAccountRepository serviceAccounts,
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
        ServiceAccount? account = null;
        SubjectFacts facts;
        if (subject.ClientId is { } clientId)
        {
            account = await serviceAccounts.GetByClientIdAsync(clientId, cancellationToken);
            facts = new SubjectFacts(true, [], null, null, IsClient: true);
        }
        else
        {
            bool sessionActive = subject is { SessionId: { } sessionId, UserId: { } userId }
                && await identity.IsSessionActiveAsync(sessionId, userId, cancellationToken);
            facts = new SubjectFacts(sessionActive, subject.AuthenticationMethods, subject.AuthenticatedAt, subject.Actor, IpAddress: subject.IpAddress);
        }

        var grants = new Dictionary<Guid, AccessGrants?>();
        var decisions = new List<AccessDecision>(query.Checks.Count);
        foreach (AccessCheck check in query.Checks)
        {
            ResourceServer server = applications[check.ResourceServer ?? string.Empty];
            if (facts.SessionActive && !grants.ContainsKey(server.Id))
            {
                grants[server.Id] = await ResolveAsync(server, subject, account, cancellationToken);
            }

            decisions.Add(evaluator.Evaluate(server, grants.GetValueOrDefault(server.Id), facts, check.Permission));
        }

        return decisions;
    }

    private async Task<AccessGrants?> ResolveAsync(ResourceServer server, PdpSubject subject, ServiceAccount? account, CancellationToken cancellationToken)
    {
        if (subject.ClientId is not null)
        {
            // The service account must belong to the organization the request names.
            return account is not null && account.TenantId == subject.TenantId
                ? await evaluator.ResolveGrantsAsync(server, account, cancellationToken)
                : null;
        }

        return subject.UserId is { } userId
            ? await evaluator.ResolveGrantsAsync(server, new AccessSubject(subject.TenantId, userId), cancellationToken)
            : null;
    }
}

/// <summary>Simulator (plan D8): what a member would get, with the trace; the session is assumed and conditions are reported.</summary>
public sealed record SimulateAccessQuery(Guid TenantId, Guid ActorUserId, Guid UserId, string ResourceServer, string Permission)
    : IQuery<SimulationResponse>;

public sealed record SimulationResponse(bool Decision, string Reason, IReadOnlyList<EvaluationStep> Steps);

internal sealed class SimulateAccessQueryHandler(
    TenantAdministration administration,
    IResourceServerRepository servers,
    AccessEvaluator evaluator) : IQueryHandler<SimulateAccessQuery, SimulationResponse>
{
    public async Task<Result<SimulationResponse>> Handle(SimulateAccessQuery query, CancellationToken cancellationToken)
    {
        Result allowed = await administration.RequirePermissionAsync(query.TenantId, query.ActorUserId, TenantAdministration.ManageRoles, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        if (await servers.GetByKeyAsync(query.ResourceServer, cancellationToken) is not { } server)
        {
            return AccessErrors.ResourceServerNotFound;
        }

        AccessGrants? grants = await evaluator.ResolveGrantsAsync(server, new AccessSubject(query.TenantId, query.UserId), cancellationToken);
        var trace = new EvaluationTrace();
        AccessDecision decision = evaluator.Evaluate(server, grants, new SubjectFacts(true, [], null, null, Assumed: true), query.Permission, trace);
        return new SimulationResponse(decision.Decision, decision.Reason, trace.Steps);
    }
}
