using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Application.Users.ProfileFields;
using Herensuge.Modules.Identity.Domain.Users;
using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Identity.UnitTests;

/// <summary>Extra registration data an application asks for: validation, storage on the user and encryption.</summary>
public sealed class ProfileFieldTests
{
    private static readonly ProfileField Company = new("company", "Empresa", ProfileFieldTypes.Text, Required: true, []);
    private static readonly ProfileField Size = new("size", "Tamaño", ProfileFieldTypes.Select, Required: false, ["1-10", "11-50"]);
    private static readonly ProfileField Terms = new("terms", "Acepto las condiciones", ProfileFieldTypes.Checkbox, Required: true, []);
    private static readonly ProfileField Employees = new("employees", "Empleados", ProfileFieldTypes.Number, Required: false, []);
    private static readonly ProfileField Founded = new("founded", "Fundación", ProfileFieldTypes.Date, Required: false, []);
    private static readonly ProfileField Phone = new("phone", "Teléfono", ProfileFieldTypes.Phone, Required: false, []);

    [Fact]
    public void Valid_values_are_normalized_and_unknown_keys_ignored()
    {
        var values = ProfileFieldValues.Validate(
            [Company, Size, Terms, Employees, Founded, Phone],
            Input(("company", "  Acme  "), ("size", "1-10"), ("terms", "on"), ("employees", "12"), ("founded", "2020-02-29"), ("phone", "+34 600 00 00 00"), ("isAdmin", "true")));

        Assert.True(values.IsSuccess);
        Assert.Equal("Acme", values.Value["company"]);
        Assert.Equal("true", values.Value["terms"]);
        Assert.Equal("+34 600 00 00 00", values.Value["phone"]);
        Assert.False(values.Value.ContainsKey("isAdmin"));
    }

    [Fact]
    public void Missing_required_values_are_rejected()
    {
        Assert.Equal(UserErrors.ProfileFieldRequired, ProfileFieldValues.Validate([Company], Input(("company", "   "))).Error);
        Assert.Equal(UserErrors.ProfileFieldRequired, ProfileFieldValues.Validate([Company], null).Error);
        Assert.Equal(UserErrors.ProfileFieldRequired, ProfileFieldValues.Validate([Terms], Input(("terms", "false"))).Error);
    }

    [Theory]
    [InlineData("size", "500+")]
    [InlineData("employees", "doce")]
    [InlineData("founded", "29/02/2020")]
    [InlineData("founded", "2021-02-29")]
    [InlineData("phone", "llámame")]
    public void Values_of_the_wrong_type_are_rejected(string key, string value) =>
        Assert.Equal(UserErrors.InvalidProfileField, ProfileFieldValues.Validate([Size, Employees, Founded, Phone], Input((key, value))).Error);

    [Fact]
    public void An_empty_optional_value_removes_the_field_and_others_are_kept()
    {
        User user = User.Register(Email.Create("ana@example.com").Value, "Ana", "hash", DateTime.UtcNow);
        Assert.True(user.UpdateProfileFields(new Dictionary<string, string> { ["company"] = "Acme", ["size"] = "1-10" }).IsSuccess);
        IReadOnlyDictionary<string, string> before = user.ProfileFields;

        Assert.True(user.UpdateProfileFields(new Dictionary<string, string> { ["size"] = string.Empty }).IsSuccess);

        Assert.Equal(new Dictionary<string, string> { ["company"] = "Acme" }, user.ProfileFields);
        Assert.NotSame(before, user.ProfileFields);
    }

    [Fact]
    public void Too_long_values_and_too_many_fields_are_rejected()
    {
        User user = User.Register(Email.Create("ana@example.com").Value, "Ana", "hash", DateTime.UtcNow);

        Assert.Equal(UserErrors.InvalidProfileField, user.UpdateProfileFields(new Dictionary<string, string> { ["bio"] = new('x', User.MaxProfileFieldLength + 1) }).Error);
        Assert.Equal(
            UserErrors.TooManyProfileFields,
            user.UpdateProfileFields(Enumerable.Range(0, User.MaxProfileFields + 1).ToDictionary(index => $"f{index}", _ => "x")).Error);
        Assert.Empty(user.ProfileFields);
    }

    [Fact]
    public void The_column_is_encrypted_json_and_empty_when_there_is_no_data()
    {
        var encryptor = new AesGcmDataEncryptor(TestKeys.Keys);
        var values = new Dictionary<string, string> { ["company"] = "Acme" };

        string stored = ProfileFieldsColumn.Write(values, encryptor);

        Assert.StartsWith("enc:v1:", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme", stored, StringComparison.Ordinal);
        Assert.Equal(values, ProfileFieldsColumn.Read(stored, encryptor));
        Assert.Equal(string.Empty, ProfileFieldsColumn.Write(new Dictionary<string, string>(), encryptor));
        Assert.Empty(ProfileFieldsColumn.Read(string.Empty, encryptor));
    }

    private static Dictionary<string, string?> Input(params (string Key, string? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value);
}
