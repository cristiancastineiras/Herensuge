using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain;

public static class AccessErrors
{
    public static readonly Error ResourceServerNotFound = Error.NotFound("Access.ResourceServerNotFound", "The application does not exist.");

    public static readonly Error NotYourResourceServer = Error.Forbidden(
        "Access.NotYourResourceServer",
        "This client can only manage and ask about its own application.");

    public static readonly Error ClientInUse = Error.Conflict(
        "Access.ClientInUse",
        "That OIDC client already belongs to another application: one application, one client.");

    // Same code as Tenancy, so the web shows the same message.
    public static readonly Error NotAllowed = Error.Forbidden("Memberships.NotAllowed", "Your role does not allow this operation.");

    public static readonly Error RoleNotFound = Error.NotFound("Access.RoleNotFound", "The role does not exist.");

    public static readonly Error RoleNameInUse = Error.Conflict("Access.RoleNameInUse", "The organization already has a role with that name.");

    public static readonly Error InvalidRoleName = Error.Validation("Access.InvalidRoleName", "The role name is required (max 100 characters).");

    public static readonly Error TooManyPermissions = Error.Validation(
        "Access.TooManyPermissions",
        $"A role can have at most {Roles.Role.MaxPermissions} permissions.");

    public static readonly Error UnknownPermission = Error.Validation("Access.UnknownPermission", "One of the permissions does not exist or was retired.");

    public static readonly Error TooManyRoles = Error.Failure("Access.TooManyRoles", $"An organization can have at most {Roles.Role.MaxPerTenant} roles.");

    public static readonly Error NotAMember = Error.NotFound("Access.NotAMember", "That person is not a member of the organization.");

    public static readonly Error InvalidAssignment = Error.Validation(
        "Access.InvalidAssignment",
        "Each assignment needs exactly one role or application role, and a future expiry date if any.");

    public static Error InvalidManifest(string reason) => Error.Failure("Access.InvalidManifest", reason);
}
