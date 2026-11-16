using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Contracts;
using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.ResourceServers;

namespace Herensuge.Modules.Access.Infrastructure.PublicApi;

internal sealed class AccessModuleApi(IResourceServerRepository servers, AccessResolver resolver) : IAccessModuleApi
{
    private const string AudienceSuffix = "-api";

    public async Task<AccessTokenClaims?> GetTokenClaimsAsync(
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

        if (application is null)
        {
            return null;
        }

        EffectiveAccess? access = await resolver.ResolveAsync(application, tenantId, userId, cancellationToken);
        return access is null ? null : new AccessTokenClaims(access.Roles, application.EmbedPermissions ? access.Permissions : null);
    }
}
