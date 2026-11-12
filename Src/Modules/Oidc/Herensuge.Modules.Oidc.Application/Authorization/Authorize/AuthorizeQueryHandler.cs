using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Contracts;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Oidc.Application.Authorization.Authorize;

/// <summary>
/// Decides what /connect/authorize does for a signed-in user: issue a code, or send them to pick an
/// organization, create one, or complete the multi-factor authentication the organization requires.
/// </summary>
internal sealed class AuthorizeQueryHandler(IIdentityModuleApi identity, ITenancyModuleApi tenancy)
    : IQueryHandler<AuthorizeQuery, AuthorizationDecision>
{
    public async Task<Result<AuthorizationDecision>> Handle(AuthorizeQuery query, CancellationToken cancellationToken)
    {
        if (!await identity.ValidateSessionAsync(query.SessionId, query.UserId, cancellationToken))
        {
            return new AuthorizationDecision(AuthorizationOutcome.SessionInvalid);
        }

        UserSnapshot? user = await identity.GetUserAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthorizationDecision(AuthorizationOutcome.SessionInvalid);
        }

        IReadOnlyList<MembershipSnapshot> memberships = await tenancy.GetMembershipsAsync(user.Id, cancellationToken);
        MembershipSnapshot[] active = [.. memberships.Where(membership => membership.TenantIsActive)];

        MembershipSnapshot? membership;
        if (query.RequestedTenantId is { } requested)
        {
            membership = active.FirstOrDefault(candidate => candidate.TenantId == requested);
            if (membership is null)
            {
                return new AuthorizationDecision(AuthorizationOutcome.TenantForbidden);
            }
        }
        else
        {
            membership = active.FirstOrDefault(candidate => candidate.TenantId == query.SelectedTenantId)
                ?? (active.Length == 1 ? active[0] : null);
        }

        if (membership is null)
        {
            AuthorizationOutcome outcome = active.Length > 1 ? AuthorizationOutcome.SelectTenant
                : memberships.Count == 0 ? AuthorizationOutcome.Onboarding
                : AuthorizationOutcome.TenantForbidden;
            return new AuthorizationDecision(outcome);
        }

        if (membership.TenantRequiresMfa && !query.AuthenticationMethods.Contains(AuthenticationMethods.MultiFactor))
        {
            return new AuthorizationDecision(user.HasMfa ? AuthorizationOutcome.MfaRequired : AuthorizationOutcome.MfaSetupRequired);
        }

        return new AuthorizationDecision(
            AuthorizationOutcome.Granted,
            new AuthorizedUser(user.Id, user.Email, user.EmailVerified, user.DisplayName, user.IsPlatformAdmin, membership.TenantId, membership.Role));
    }
}
