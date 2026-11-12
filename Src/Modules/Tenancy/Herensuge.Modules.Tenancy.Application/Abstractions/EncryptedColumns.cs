namespace Herensuge.Modules.Tenancy.Application.Abstractions;

/// <summary>
/// Columns of the tenancy schema stored encrypted (AES-256-GCM, see IDataEncryptor). Organization names and slugs
/// are not personal data and stay searchable. Never change a value: it is authenticated with the data.
/// </summary>
public static class EncryptedColumns
{
    public const string InvitationEmail = "tenancy.invitations.email";
}
