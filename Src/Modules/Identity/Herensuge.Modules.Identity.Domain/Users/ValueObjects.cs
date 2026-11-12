using System.Net.Mail;
using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Domain.Users;

/// <summary>Normalized (trimmed, lower case) email address.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > MaxLength
            || !MailAddress.TryCreate(normalized, out MailAddress? address)
            || !string.Equals(address.Address, normalized, StringComparison.Ordinal)
            || !address.Host.Contains('.', StringComparison.Ordinal))
        {
            return UserErrors.InvalidEmail;
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}

/// <summary>Phone number in E.164 format (+34600000000).</summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PhoneNumber> Create(string? value)
    {
        string compact = new([.. (value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c is not '-' and not '(' and not ')' and not '.')]);
        if (compact.Length is < 8 or > 16 || compact[0] != '+' || compact[1] == '0' || !compact.Skip(1).All(char.IsAsciiDigit))
        {
            return UserErrors.InvalidPhoneNumber;
        }

        return new PhoneNumber(compact);
    }

    /// <summary>+34•••••••000: enough for the user to recognise the number.</summary>
    public static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 6 ? phoneNumber : $"{phoneNumber[..3]}{new string('•', phoneNumber.Length - 6)}{phoneNumber[^3..]}";

    public override string ToString() => Value;
}
