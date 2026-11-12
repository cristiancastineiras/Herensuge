namespace Herensuge.Modules.Tenancy.Application.Invitations.GetInvitationPreview;

/// <summary>What the invitation link shows before signing in or registering.</summary>
public sealed record GetInvitationPreviewQuery(string Token) : IQuery<InvitationPreviewResponse>;

public sealed record InvitationPreviewResponse(string TenantName, string Email, string Role, bool IsValid, DateTime ExpiresAtUtc);
