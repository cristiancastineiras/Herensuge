namespace Herensuge.Modules.Oidc.Application.Authorization;

/// <summary>Everything needed to build the claims of the tokens issued for one organization.</summary>
public sealed record AuthorizedUser(
    Guid UserId,
    string Email,
    bool EmailVerified,
    string DisplayName,
    bool IsPlatformAdmin,
    Guid TenantId,
    string TenantRole);
