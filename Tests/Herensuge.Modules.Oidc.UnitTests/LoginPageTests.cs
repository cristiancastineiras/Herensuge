using FluentValidation.Results;
using Herensuge.Common.Application.Security;
using Herensuge.Modules.Oidc.Application.Clients.LoginPage;
using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Oidc.UnitTests;

/// <summary>Login page of a client: what /connect/authorize enforces and what the platform may save.</summary>
public sealed class LoginPageTests
{
    private static readonly LoginMethods PasswordOnly = new(Password: true, Passkey: false, Google: false, Microsoft: false);
    private static readonly LoginMethods MicrosoftOnly = new(Password: false, Passkey: false, Google: false, Microsoft: true);

    [Fact]
    public void A_session_passes_only_with_a_first_factor_the_client_accepts()
    {
        Assert.True(LoginRules.AcceptsSession(PasswordOnly, [AuthenticationMethods.Password, AuthenticationMethods.Otp, AuthenticationMethods.MultiFactor]));
        Assert.False(LoginRules.AcceptsSession(PasswordOnly, [AuthenticationMethods.Passkey, AuthenticationMethods.MultiFactor]));
        Assert.False(LoginRules.AcceptsSession(PasswordOnly, [AuthenticationMethods.Federated]));
        Assert.True(LoginRules.AcceptsSession(MicrosoftOnly, [AuthenticationMethods.Federated]));
        Assert.False(LoginRules.AcceptsSession(MicrosoftOnly, [AuthenticationMethods.Password]));
    }

    [Fact]
    public void A_support_session_always_passes() =>
        Assert.True(LoginRules.AcceptsSession(PasswordOnly, [AuthenticationMethods.Passkey, AuthenticationMethods.Impersonation]));

    [Fact]
    public void The_profile_is_complete_when_every_required_field_has_a_value()
    {
        LoginSettings settings = WithFields(
            new ProfileField("company", "Empresa", ProfileFieldTypes.Text, Required: true, []),
            new ProfileField("terms", "Condiciones", ProfileFieldTypes.Checkbox, Required: true, []),
            new ProfileField("size", "Tamaño", ProfileFieldTypes.Text, Required: false, []));

        Assert.False(LoginRules.IsProfileComplete(settings, Values(("company", "Acme"))));
        Assert.False(LoginRules.IsProfileComplete(settings, Values(("company", "Acme"), ("terms", "false"))));
        Assert.True(LoginRules.IsProfileComplete(settings, Values(("company", "Acme"), ("terms", "true"))));
        Assert.True(LoginRules.IsProfileComplete(LoginSettings.Default, Values()));
    }

    [Fact]
    public void An_application_only_sees_its_own_fields()
    {
        LoginSettings settings = WithFields(new ProfileField("company", "Empresa", ProfileFieldTypes.Text, Required: false, []));

        Dictionary<string, string> values = LoginRules.ClientValues(settings, Values(("company", "Acme"), ("salary", "secret")));

        Assert.Equal(Values(("company", "Acme")), values);
    }

    [Fact]
    public void The_default_settings_are_valid() => Assert.True(Validate(LoginSettings.Default).IsValid);

    [Fact]
    public void A_full_page_is_valid()
    {
        LoginSettings settings = LoginSettings.Default with
        {
            Branding = new LoginBranding("data:image/png;base64,iVBORw0KGgo=", "#1d4ed8", "https://example.com/terms", "https://example.com/privacy"),
            Methods = MicrosoftOnly,
            AllowRegistration = false,
            ProfileFields =
            [
                new ProfileField("company", "Empresa", ProfileFieldTypes.Text, Required: true, []),
                new ProfileField("company_size", "Tamaño", ProfileFieldTypes.Select, Required: false, ["1-10", "11-50"]),
            ],
        };

        Assert.True(Validate(settings).IsValid);
    }

    [Fact]
    public void At_least_one_method_must_be_enabled() =>
        Assert.False(Validate(LoginSettings.Default with { Methods = new LoginMethods(false, false, false, false) }).IsValid);

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=", null)]
    [InlineData(null, "blue")]
    [InlineData(null, "#12345")]
    public void Logos_must_be_inline_images_and_colors_hex(string? logo, string? color) =>
        Assert.False(Validate(LoginSettings.Default with { Branding = new LoginBranding(logo, color, null, null) }).IsValid);

    [Fact]
    public void Links_must_be_https()
    {
        Assert.False(Validate(LoginSettings.Default with { Branding = new LoginBranding(null, null, "http://example.com/terms", null) }).IsValid);
        Assert.False(Validate(LoginSettings.Default with { Branding = new LoginBranding(null, null, null, null, "http://fichajes.example.com") }).IsValid);
        Assert.True(Validate(LoginSettings.Default with { Branding = new LoginBranding(null, null, null, null, "https://fichajes.example.com/") }).IsValid);
    }

    [Fact]
    public void Pages_saved_before_the_organization_option_still_let_people_create_one()
    {
        const string saved = """
            {
              "branding": { "logo": null, "primaryColor": "#0f766e", "termsUrl": null, "privacyUrl": null },
              "methods": { "password": true, "passkey": true, "google": false, "microsoft": false },
              "allowRegistration": true,
              "profileFields": []
            }
            """;

        LoginSettings settings = System.Text.Json.JsonSerializer.Deserialize<LoginSettings>(saved, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.True(settings.AllowTenantCreation);
        Assert.Null(settings.Branding.HomeUrl);
    }

    [Theory]
    [InlineData("Company", ProfileFieldTypes.Text)]
    [InlineData("company", "color")]
    public void Field_keys_and_types_are_checked(string key, string type) =>
        Assert.False(Validate(WithFields(new ProfileField(key, "Empresa", type, Required: false, []))).IsValid);

    [Fact]
    public void Only_select_fields_have_options_and_they_need_at_least_one()
    {
        Assert.False(Validate(WithFields(new ProfileField("size", "Tamaño", ProfileFieldTypes.Select, Required: false, []))).IsValid);
        Assert.False(Validate(WithFields(new ProfileField("size", "Tamaño", ProfileFieldTypes.Text, Required: false, ["a"]))).IsValid);
        Assert.False(Validate(WithFields(new ProfileField("size", "Tamaño", ProfileFieldTypes.Select, Required: false, ["a", "a"]))).IsValid);
    }

    [Fact]
    public void Field_keys_are_unique() =>
        Assert.False(Validate(WithFields(
            new ProfileField("company", "Empresa", ProfileFieldTypes.Text, Required: false, []),
            new ProfileField("company", "Compañía", ProfileFieldTypes.Text, Required: false, []))).IsValid);

    private static ValidationResult Validate(LoginSettings settings) =>
        new UpdateLoginSettingsCommandValidator().Validate(new UpdateLoginSettingsCommand("app", settings));

    private static LoginSettings WithFields(params ProfileField[] fields) => LoginSettings.Default with { ProfileFields = fields };

    private static Dictionary<string, string> Values(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
}
