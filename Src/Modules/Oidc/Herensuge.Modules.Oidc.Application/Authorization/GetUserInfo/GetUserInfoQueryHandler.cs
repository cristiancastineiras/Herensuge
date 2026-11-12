using Herensuge.Modules.Identity.Contracts;

namespace Herensuge.Modules.Oidc.Application.Authorization.GetUserInfo;

internal sealed class GetUserInfoQueryHandler(IIdentityModuleApi identity) : IQueryHandler<GetUserInfoQuery, UserInfoResponse>
{
    public async Task<Result<UserInfoResponse>> Handle(GetUserInfoQuery query, CancellationToken cancellationToken)
    {
        UserSnapshot? user = await identity.GetUserAsync(query.UserId, cancellationToken);
        return user is null
            ? OidcErrors.SessionInvalid
            : new UserInfoResponse(user.Id, user.Email, user.EmailVerified, user.DisplayName);
    }
}
