using Herensuge.Common.Application.Security;
using Herensuge.Modules.Identity.Contracts;
using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Oidc.Application.Authorization.ValidateTokenRequest;

internal sealed class ValidateTokenRequestQueryHandler(IIdentityModuleApi identity, ITenancyModuleApi tenancy)
    : IQueryHandler<ValidateTokenRequestQuery, AuthorizedUser>
{
    public async Task<Result<AuthorizedUser>> Handle(ValidateTokenRequestQuery query, CancellationToken cancellationToken)
    {
        if (!await identity.ValidateSessionAsync(query.SessionId, query.UserId, cancellationToken))
        {
            return OidcErrors.SessionInvalid;
        }

        UserSnapshot? user = await identity.GetUserAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return OidcErrors.SessionInvalid;
        }

        MembershipSnapshot? membership = await tenancy.GetMembershipAsync(query.TenantId, query.UserId, cancellationToken);
        if (membership is null)
        {
            return OidcErrors.MembershipRevoked;
        }

        if (!membership.TenantIsActive)
        {
            return OidcErrors.TenantInactive;
        }

        // The organization may have turned on mandatory MFA after this session started.
        if (membership.TenantRequiresMfa && !query.AuthenticationMethods.Contains(AuthenticationMethods.MultiFactor))
        {
            return OidcErrors.MfaRequired;
        }

        return new AuthorizedUser(user.Id, user.Email, user.EmailVerified, user.DisplayName, user.IsPlatformAdmin, membership.TenantId, membership.Role);
    }
}
