using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Domain.ServiceAccounts;

namespace Herensuge.Modules.Access.Infrastructure.PublicApi;

internal sealed class AccessModuleApi(
    IResourceServerRepository servers,
    IServiceAccountRepository serviceAccounts,
    AccessEvaluator evaluator) : IAccessModuleApi
{
    private const string AudienceSuffix = "-api";

    public async Task<AccessClaims?> GetAccessClaimsAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken)
    {
        if (await ApplicationOfAsync(audiences, cancellationToken) is not { } application
            || await evaluator.ResolveGrantsAsync(application, new AccessSubject(tenantId, userId), cancellationToken) is not { } grants)
        {
            return null;
        }

        return ToClaims(application, grants);
    }

    public async Task<ServiceAccountClaims?> GetServiceAccountClaimsAsync(
        string clientId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken)
    {
        if (await serviceAccounts.GetByClientIdAsync(clientId, cancellationToken) is not { } account
            || await ApplicationOfAsync(audiences, cancellationToken) is not { } application
            || await evaluator.ResolveGrantsAsync(application, account, cancellationToken) is not { } grants)
        {
            return null;
        }

        return new ServiceAccountClaims(account.TenantId, ToClaims(application, grants));
    }

    public async Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string resourceServer, string permission, CancellationToken cancellationToken)
    {
        if (await servers.GetByKeyAsync(resourceServer, cancellationToken) is not { } server)
        {
            return false;
        }

        AccessGrants? grants = await evaluator.ResolveGrantsAsync(server, new AccessSubject(tenantId, userId), cancellationToken);
        return evaluator.Evaluate(server, grants, SubjectFacts.ValidatedSession, permission).Decision;
    }

    private static AccessClaims ToClaims(ResourceServer application, AccessGrants grants)
    {
        AccessClaimSet claims = AccessEvaluator.ToClaims(application, grants);
        return new AccessClaims(claims.Roles, claims.Permissions, claims.ConditionalPermissions, claims.Entitlements, application.EmbedPermissions);
    }

    /// <summary>The Access application behind the audiences; null with none or with two (one token, one API: ADR-A4).</summary>
    private async Task<ResourceServer?> ApplicationOfAsync(IReadOnlyCollection<string> audiences, CancellationToken cancellationToken)
    {
        ResourceServer? application = null;
        foreach (string audience in audiences.Where(audience => audience.EndsWith(AudienceSuffix, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            if (await servers.GetByKeyAsync(audience[..^AudienceSuffix.Length], cancellationToken) is not { } server)
            {
                continue;
            }

            if (application is not null)
            {
                return null;
            }

            application = server;
        }

        return application;
    }
}
