using System.Globalization;
using System.Text.RegularExpressions;
using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Identity.Application.Users.ProfileFields;

/// <summary>
/// Checks what a form sent against the fields an application asks for. Returns one value per field of the application
/// (an empty one removes it from the user); anything else in the form is ignored.
/// </summary>
internal static partial class ProfileFieldValues
{
    public static Result<Dictionary<string, string>> Validate(IReadOnlyList<ProfileField> fields, IReadOnlyDictionary<string, string?>? input)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ProfileField field in fields)
        {
            string value = (input is not null && input.TryGetValue(field.Key, out string? raw) ? raw : null)?.Trim() ?? string.Empty;
            if (field.Type == ProfileFieldTypes.Checkbox)
            {
                value = value is "true" or "on" ? "true" : "false";
            }

            if (field.IsMissingIn(new Dictionary<string, string> { [field.Key] = value }))
            {
                return UserErrors.ProfileFieldRequired;
            }

            if (value.Length > 0 && !IsValid(field, value))
            {
                return UserErrors.InvalidProfileField;
            }

            values[field.Key] = value;
        }

        return values;
    }

    private static bool IsValid(ProfileField field, string value) =>
        value.Length <= User.MaxProfileFieldLength
        && field.Type switch
        {
            ProfileFieldTypes.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            ProfileFieldTypes.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            ProfileFieldTypes.Phone => Phone().IsMatch(value),
            ProfileFieldTypes.Select => field.Options.Contains(value, StringComparer.Ordinal),
            ProfileFieldTypes.Checkbox => value is "true" or "false",
            _ => true,
        };

    /// <summary>Loose on purpose (spaces, dashes, brackets): it is contact data, not the verified MFA phone.</summary>
    [GeneratedRegex(@"^\+?[0-9][0-9 ()\-]{5,24}$")]
    private static partial Regex Phone();
}
