namespace Herensuge.Modules.Tenancy.Application.Members.RemoveMember;

/// <summary>Removes a member; when ActorUserId equals UserId the member is leaving.</summary>
public sealed record RemoveMemberCommand(Guid TenantId, Guid ActorUserId, Guid UserId) : ICommand;
