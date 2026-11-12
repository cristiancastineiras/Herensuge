namespace Herensuge.Modules.Tenancy.Application.Members.GetMembers;

public sealed record GetMembersQuery(Guid TenantId) : IQuery<IReadOnlyList<MemberResponse>>;

public sealed record MemberResponse(Guid UserId, string Email, string DisplayName, string Role, DateTime JoinedAtUtc);
