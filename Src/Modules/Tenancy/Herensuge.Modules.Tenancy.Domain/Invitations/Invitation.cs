using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Herensuge.Common.Domain;
using Herensuge.Modules.Tenancy.Domain.Invitations.Events;
using Herensuge.Modules.Tenancy.Domain.Memberships;

namespace Herensuge.Modules.Tenancy.Domain.Invitations;

/// <summary>Single-use invitation to join an organization. Only the hash of its token is stored.</summary>
public sealed class Invitation : AggregateRoot
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public TenantRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    public InvitationStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    /// <param name="email">Normalized (trimmed, lower case) address.</param>
    public static (Invitation Invitation, string Token) Create(
        Guid tenantId,
        string tenantName,
        string email,
        TenantRole role,
        Guid invitedByUserId,
        DateTime utcNow)
    {
        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var invitation = new Invitation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Email = email,
            Role = role,
            TokenHash = HashToken(token),
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow + Lifetime,
        };

        invitation.Raise(new InvitationCreatedDomainEvent(
            invitation.Id, tenantId, tenantName, email, role, invitedByUserId, token, invitation.ExpiresAtUtc));
        return (invitation, token);
    }

    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    public bool IsPending(DateTime utcNow) => Status == InvitationStatus.Pending && utcNow < ExpiresAtUtc;

    /// <summary>Only a user whose verified email is the invited one can accept it.</summary>
    public Result Accept(Guid userId, string userEmail, bool emailVerified, DateTime utcNow)
    {
        if (!IsPending(utcNow))
        {
            return InvitationErrors.NotPending;
        }

        if (!emailVerified)
        {
            return InvitationErrors.EmailNotVerified;
        }

        if (!string.Equals(Email, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return InvitationErrors.EmailMismatch;
        }

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = utcNow;
        AcceptedByUserId = userId;
        Raise(new InvitationAcceptedDomainEvent(Id, TenantId, userId));
        return Result.Success();
    }

    public Result Revoke()
    {
        if (Status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPending;
        }

        Status = InvitationStatus.Revoked;
        Raise(new InvitationRevokedDomainEvent(Id, TenantId));
        return Result.Success();
    }
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
}

public static class InvitationErrors
{
    public static readonly Error NotFound = Error.NotFound("Invitations.NotFound", "The invitation does not exist.");

    public static readonly Error NotPending = Error.Failure("Invitations.NotPending", "The invitation was already used, revoked or has expired.");

    public static readonly Error EmailNotVerified = Error.Forbidden("Invitations.EmailNotVerified", "Verify your email address before accepting the invitation.");

    public static readonly Error EmailMismatch = Error.Forbidden(
        "Invitations.EmailMismatch",
        "The invitation was sent to a different email address. Sign in with that address.");
}
