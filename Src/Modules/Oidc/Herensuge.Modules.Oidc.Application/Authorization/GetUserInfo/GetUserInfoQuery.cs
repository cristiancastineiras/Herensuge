namespace Herensuge.Modules.Oidc.Application.Authorization.GetUserInfo;

public sealed record GetUserInfoQuery(Guid UserId) : IQuery<UserInfoResponse>;

public sealed record UserInfoResponse(Guid UserId, string Email, bool EmailVerified, string DisplayName);
