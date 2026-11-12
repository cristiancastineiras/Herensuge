using Herensuge.Modules.Tenancy.Domain.Invitations;
using Herensuge.Modules.Tenancy.Domain.Memberships;
using Herensuge.Modules.Tenancy.Domain.Tenants;
using Herensuge.Modules.Tenancy.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Tenancy.Infrastructure.Repositories;

internal sealed class TenantRepository(TenancyDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Tenants.SingleOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Tenants.AnyAsync(tenant => tenant.Slug == slug, cancellationToken);

    public void Add(Tenant tenant) => context.Tenants.Add(tenant);
}

internal sealed class MembershipRepository(TenancyDbContext context) : IMembershipRepository
{
    public Task<Membership?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        context.Memberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId && membership.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<Membership>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Memberships.Where(membership => membership.UserId == userId).ToListAsync(cancellationToken);

    public Task<int> CountOwnersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Memberships.CountAsync(
            membership => membership.TenantId == tenantId && membership.Role == TenantRole.Owner,
            cancellationToken);

    public void Add(Membership membership) => context.Memberships.Add(membership);

    public void Remove(Membership membership) => context.Memberships.Remove(membership);
}

internal sealed class InvitationRepository(TenancyDbContext context) : IInvitationRepository
{
    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Invitations.SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);

    public Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Invitations.SingleOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> GetPendingByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken) =>
        await context.Invitations
            .Where(invitation => invitation.TenantId == tenantId && invitation.Email == email && invitation.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

    public void Add(Invitation invitation) => context.Invitations.Add(invitation);
}
