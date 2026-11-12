namespace Herensuge.Common.Infrastructure.Messaging;

/// <summary>
/// Outbox and inbox rows carry personal data and secrets (verification links, SMS codes): their content, IP address
/// and user agent are encrypted with a purpose per schema, table and column.
/// </summary>
internal static class MessageEncryption
{
    public const string OutboxTable = "outbox_messages";
    public const string InboxTable = "inbox_messages";

    public static string Purpose(string schema, string table, string column) => $"{schema}.{table}.{column}";
}
