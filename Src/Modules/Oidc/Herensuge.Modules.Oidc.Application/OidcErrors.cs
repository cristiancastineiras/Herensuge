namespace Herensuge.Modules.Oidc.Application;

public static class OidcErrors
{
    public static readonly Error SessionInvalid = Error.Unauthorized("Oidc.SessionInvalid", "The sign-in session has expired or was revoked.");

    public static readonly Error MembershipRevoked = Error.Forbidden("Oidc.MembershipRevoked", "The user no longer belongs to the organization.");

    public static readonly Error TenantInactive = Error.Forbidden("Oidc.TenantInactive", "The organization is deactivated.");

    public static readonly Error MfaRequired = Error.Forbidden("Oidc.MfaRequired", "The organization requires multi-factor authentication.");

    public static readonly Error ClientNotFound = Error.NotFound("Clients.NotFound", "The client does not exist.");

    public static readonly Error ClientAlreadyExists = Error.Conflict("Clients.AlreadyExists", "A client with that id already exists.");

    public static readonly Error SystemClient = Error.Forbidden("Clients.SystemClient", "This client is managed through configuration.");

    public static readonly Error PublicClientHasNoSecret = Error.Failure("Clients.PublicClientHasNoSecret", "Public clients have no secret.");
}
