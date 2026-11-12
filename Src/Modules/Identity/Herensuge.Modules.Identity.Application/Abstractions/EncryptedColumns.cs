namespace Herensuge.Modules.Identity.Application.Abstractions;

/// <summary>
/// Columns of the identity schema stored encrypted (AES-256-GCM, see IDataEncryptor). The EF configurations encrypt
/// them on write; the Dapper queries decrypt them on read. Never change a value: it is authenticated with the data.
/// </summary>
public static class EncryptedColumns
{
    public const string UserEmail = "identity.users.email";
    public const string UserDisplayName = "identity.users.display_name";
    public const string UserPhoneNumber = "identity.users.phone_number";
    public const string ExternalLoginEmail = "identity.user_external_logins.email";
    public const string PasskeyName = "identity.user_passkeys.name";
    public const string SessionIpAddress = "identity.user_sessions.ip_address";
    public const string SessionUserAgent = "identity.user_sessions.user_agent";
    public const string TokenTarget = "identity.one_time_tokens.target";

    /// <summary>Blind index of the email (keyed hash): the only way to find a user by email.</summary>
    public const string UserEmailIndex = "identity.users.email_index";
}
