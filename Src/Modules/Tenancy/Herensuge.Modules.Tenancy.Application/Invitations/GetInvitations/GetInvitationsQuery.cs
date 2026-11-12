namespace Herensuge.Modules.Tenancy.Application.Invitations.GetInvitations;

/// <summary>Pending (not expired) invitations of an organization.</summary>
public sealed record GetInvitationsQuery(Guid TenantId) : IQuery<IReadOnlyList<InvitationResponse>>;

public sealed record InvitationResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid InvitedByUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }
}
