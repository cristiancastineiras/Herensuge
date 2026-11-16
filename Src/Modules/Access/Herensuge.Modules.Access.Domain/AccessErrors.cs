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

    public static readonly Error ReservedResourceServer = Error.Forbidden(
        "Access.ReservedResourceServer",
        "That application belongs to Herensuge and is synced only by Herensuge itself.");

    public static readonly Error GroupNotFound = Error.NotFound("Access.GroupNotFound", "The group does not exist.");

    public static readonly Error InvalidGroupName = Error.Validation("Access.InvalidGroupName", "The group name is required (max 100 characters).");

    public static readonly Error GroupNameInUse = Error.Conflict("Access.GroupNameInUse", "The organization already has a group with that name.");

    public static readonly Error TooManyGroups = Error.Failure("Access.TooManyGroups", $"An organization can have at most {Groups.Group.MaxPerTenant} groups.");

    public static readonly Error TooManyGroupMembers = Error.Validation(
        "Access.TooManyGroupMembers",
        $"A group can have at most {Groups.Group.MaxMembers} members.");

    public static readonly Error ServiceAccountNotFound = Error.NotFound("Access.ServiceAccountNotFound", "The service account does not exist.");

    public static readonly Error InvalidServiceAccountName = Error.Validation(
        "Access.InvalidServiceAccountName",
        "The service account name is required (max 100 characters).");

    public static readonly Error TooManyServiceAccounts = Error.Failure(
        "Access.TooManyServiceAccounts",
        $"An organization can have at most {ServiceAccounts.ServiceAccount.MaxPerTenant} service accounts.");

    public static readonly Error PlanNotFound = Error.NotFound("Access.PlanNotFound", "The plan does not exist.");

    public static readonly Error PlanKeyInUse = Error.Conflict("Access.PlanKeyInUse", "There is already a plan with that key.");

    public static readonly Error DefaultPlanRequired = Error.Conflict(
        "Access.DefaultPlanRequired",
        "Mark another plan as the default before deleting this one.");

    public static readonly Error UnknownFeature = Error.Validation("Access.UnknownFeature", "One of the features does not exist in its application.");

    public static readonly Error RelationshipsDisabled = Error.Conflict(
        "Access.RelationshipsDisabled",
        "Per-object permissions are not enabled in this installation (Access:OpenFga:ApiUrl).");

    public static readonly Error RelationshipsUnavailable = Error.Failure(
        "Access.RelationshipsUnavailable",
        "The relationship store did not answer. Try again in a moment.");

    public static Error InvalidRelationship(string reason) => Error.Validation("Access.InvalidRelationship", reason);

    public static Error InvalidPlan(string reason) => Error.Validation("Access.InvalidPlan", reason);

    public static Error InvalidManifest(string reason) => Error.Failure("Access.InvalidManifest", reason);

    /// <summary>The actor tried to grant permissions it does not hold itself.</summary>
    public static Error CannotGrant(IEnumerable<string> missing) => Error.Forbidden(
        "Access.CannotGrant",
        $"You can only grant permissions you hold. Missing: {string.Join(", ", missing)}.");
}
