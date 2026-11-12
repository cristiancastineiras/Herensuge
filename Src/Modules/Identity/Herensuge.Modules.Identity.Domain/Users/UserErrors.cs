using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Users;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("Users.NotFound", "The user does not exist.");

    public static readonly Error InvalidEmail = Error.Validation("Users.InvalidEmail", "The email address is not valid.");

    public static readonly Error InvalidPhoneNumber = Error.Validation(
        "Users.InvalidPhoneNumber",
        "The phone number must use the international format, for example +34600000000.");

    public static readonly Error EmailAlreadyInUse = Error.Conflict("Users.EmailAlreadyInUse", "The email address is already in use.");

    public static readonly Error InvalidCredentials = Error.Unauthorized("Users.InvalidCredentials", "The email or the password is not correct.");

    public static readonly Error EmailNotVerified = Error.Forbidden("Users.EmailNotVerified", "The email address has not been verified yet.");

    public static readonly Error LockedOut = Error.Forbidden("Users.LockedOut", "Too many failed attempts. Try again in a few minutes.");

    public static readonly Error InvalidMfaCode = Error.Unauthorized("Users.InvalidMfaCode", "The verification code is not correct.");

    public static readonly Error MfaMethodNotAvailable = Error.Validation("Users.MfaMethodNotAvailable", "That verification method is not enabled.");

    public static readonly Error MfaNotEnabled = Error.Failure("Users.MfaNotEnabled", "Multi-factor authentication is not enabled.");

    public static readonly Error MfaRequiredByOrganization = Error.Failure(
        "Users.MfaRequiredByOrganization",
        "An organization you belong to requires multi-factor authentication.");

    public static readonly Error TotpAlreadyEnabled = Error.Conflict("Users.TotpAlreadyEnabled", "The authenticator app is already enabled.");

    public static readonly Error TotpSetupNotStarted = Error.Failure("Users.TotpSetupNotStarted", "Start the authenticator app setup first.");

    public static readonly Error TotpNotEnabled = Error.Failure("Users.TotpNotEnabled", "The authenticator app is not enabled.");

    public static readonly Error PhoneNumberNotSet = Error.Failure("Users.PhoneNumberNotSet", "There is no verified phone number.");

    public static readonly Error LastAccessMethod = Error.Failure("Users.LastAccessMethod", "You cannot remove your last way of signing in.");

    public static readonly Error PasskeyNotFound = Error.NotFound("Users.PasskeyNotFound", "The passkey does not exist.");

    public static readonly Error PasskeyLimitReached = Error.Failure("Users.PasskeyLimitReached", "You have reached the maximum number of passkeys.");

    public static readonly Error PasskeyAlreadyRegistered = Error.Conflict("Users.PasskeyAlreadyRegistered", "This passkey is already registered.");

    public static readonly Error PasskeyVerificationFailed = Error.Validation("Users.PasskeyVerificationFailed", "The passkey could not be verified.");

    public static readonly Error ExternalLoginNotFound = Error.NotFound("Users.ExternalLoginNotFound", "That account is not linked.");

    public static readonly Error ExternalProviderAlreadyLinked = Error.Conflict(
        "Users.ExternalProviderAlreadyLinked",
        "An account of that provider is already linked.");

    public static readonly Error ExternalLoginInUse = Error.Conflict("Users.ExternalLoginInUse", "That external account is linked to another user.");

    public static readonly Error ExternalEmailInUse = Error.Conflict(
        "Users.ExternalEmailInUse",
        "An account with that email already exists. Sign in with it and link the provider from your account settings.");

    public static readonly Error ExternalEmailMissing = Error.Validation("Users.ExternalEmailMissing", "The provider did not share an email address.");

    public static readonly Error InvalidCurrentPassword = Error.Validation("Users.InvalidCurrentPassword", "The current password is not correct.");

    public static readonly Error LastOwnerOfOrganization = Error.Failure(
        "Users.LastOwnerOfOrganization",
        "Transfer the ownership of your organizations before deleting the account.");

    public static readonly Error CodeRequestedTooRecently = Error.TooManyRequests(
        "Users.CodeRequestedTooRecently",
        "Wait a few seconds before requesting another code.");
}
