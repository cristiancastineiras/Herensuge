namespace Herensuge.Modules.Audit.Application.Abstractions;

/// <summary>
/// Columns of audit.audit_entries stored encrypted (AES-256-GCM, see IDataEncryptor). The filters (organization,
/// user, action, date) use plain columns. Never change a value: it is authenticated with the data.
/// </summary>
public static class EncryptedColumns
{
    public const string IpAddress = "audit.audit_entries.ip_address";
    public const string UserAgent = "audit.audit_entries.user_agent";
    public const string Metadata = "audit.audit_entries.metadata";
}
