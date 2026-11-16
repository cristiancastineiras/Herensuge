using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.Infrastructure.PublicApi;

internal sealed class AccessModuleApi(IResourceServerRepository servers, AccessEvaluator evaluator) : IAccessModuleApi
{
    private const string AudienceSuffix = "-api";

    public async Task<AccessClaims?> GetAccessClaimsAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken)
    {
        ResourceServer? application = null;
        foreach (string audience in audiences.Where(audience => audience.EndsWith(AudienceSuffix, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            if (await servers.GetByKeyAsync(audience[..^AudienceSuffix.Length], cancellationToken) is not { } server)
            {
                continue;
            }

            // One token, one API (ADR-A4): with two applications the token carries neither, and their APIs ask the PDP.
            if (application is not null)
            {
                return null;
            }

            application = server;
        }

        if (application is null || await evaluator.ResolveGrantsAsync(application, new AccessSubject(tenantId, userId), cancellationToken) is not { } grants)
        {
            return null;
        }

        AccessClaimSet claims = AccessEvaluator.ToClaims(application, grants);
        return new AccessClaims(claims.Roles, claims.Permissions, claims.ConditionalPermissions, application.EmbedPermissions);
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
}
