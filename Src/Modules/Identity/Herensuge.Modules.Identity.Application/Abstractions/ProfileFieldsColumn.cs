using System.Text.Json;
using Herensuge.Common.Application.Security;

namespace Herensuge.Modules.Identity.Application.Abstractions;

/// <summary>
/// identity.users.profile_fields: the user's registration data as a JSON object, encrypted as a whole. An empty
/// string means no data (existing users, and nothing to encrypt). Used by EF (writes) and by the Dapper queries.
/// </summary>
public static class ProfileFieldsColumn
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.Ordinal);

    public static string Write(IReadOnlyDictionary<string, string> values, IDataEncryptor encryptor) =>
        values.Count == 0 ? string.Empty : encryptor.Encrypt(JsonSerializer.Serialize(values), EncryptedColumns.UserProfileFields);

    public static IReadOnlyDictionary<string, string> Read(string? stored, IDataEncryptor encryptor)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return Empty;
        }

        Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(encryptor.Decrypt(stored, EncryptedColumns.UserProfileFields));
        return values is null ? Empty : new Dictionary<string, string>(values, StringComparer.Ordinal);
    }
}
