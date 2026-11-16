using Herensuge.Modules.Access.Domain;
using Herensuge.Modules.Access.Domain.Requests;

namespace Herensuge.Modules.Access.UnitTests;

/// <summary>Temporary access with approval (plan D13).</summary>
public sealed class AccessRequestTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private static AccessRequest Pending(int hours = 8) =>
        AccessRequest.Create(Guid.NewGuid(), Ana, Guid.NewGuid(), null, hours, "cierre del trimestre", Now).Value;

    [Fact]
    public void A_request_needs_one_role_a_reason_and_1_to_72_hours()
    {
        Assert.Equal(AccessErrors.InvalidAccessRequest, AccessRequest.Create(Guid.NewGuid(), Ana, null, null, 8, "x", Now).Error);
        Assert.Equal(AccessErrors.InvalidAccessRequest, AccessRequest.Create(Guid.NewGuid(), Ana, Guid.NewGuid(), null, 73, "x", Now).Error);
        Assert.Equal(AccessErrors.InvalidAccessRequest, AccessRequest.Create(Guid.NewGuid(), Ana, Guid.NewGuid(), null, 8, " ", Now).Error);
    }

    [Fact]
    public void Approving_grants_it_for_the_requested_hours_once()
    {
        AccessRequest request = Pending(hours: 4);

        Assert.Equal(Now.AddHours(1 + 4), request.Approve(Approver, Now.AddHours(1)).Value);
        Assert.Equal(AccessRequestStatuses.Approved, request.StatusAt(Now.AddHours(30)));
        Assert.Equal(AccessErrors.AccessRequestNotPending, request.Deny(Approver, Now.AddHours(2)).Error);
    }

    [Fact]
    public void Nobody_resolves_their_own_request()
    {
        Assert.Equal(AccessErrors.CannotDecideOwnRequest, Pending().Approve(Ana, Now).Error);
    }

    [Fact]
    public void A_pending_request_expires_after_a_day()
    {
        AccessRequest request = Pending();

        Assert.Equal(AccessRequestStatuses.Expired, request.StatusAt(Now.AddHours(25)));
        Assert.Equal(AccessErrors.AccessRequestNotPending, request.Approve(Approver, Now.AddHours(25)).Error);
    }
}
