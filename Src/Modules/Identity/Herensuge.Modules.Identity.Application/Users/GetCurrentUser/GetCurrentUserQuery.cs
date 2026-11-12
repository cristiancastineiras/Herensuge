namespace Herensuge.Modules.Identity.Application.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<CurrentUserResponse>;
