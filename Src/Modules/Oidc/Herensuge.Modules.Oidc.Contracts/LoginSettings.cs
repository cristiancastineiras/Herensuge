namespace Herensuge.Modules.Oidc.Contracts;

/// <summary>
/// Login page of one client (one application of the SaaS): branding, sign-in methods and what registration asks for.
/// Stored with the client; a client that was never configured gets <see cref="Default"/>.
/// </summary>
/// <param name="AllowRegistration">
/// "Create account" on this client's login page, and new accounts through Google or Microsoft. When off, only people
/// who already have a Herensuge account (or an invitation) get in.
/// </param>
/// <param name="ProfileFields">Extra data asked at registration (and before the first sign-in if missing).</param>
/// <param name="AllowTenantCreation">
/// Whether someone without an organization may create one on the way in ("Crea tu organización"). Off for an
/// application whose companies are set up by their admins (in the portal) and whose people join by invitation: they see
/// "ask your company to invite you" instead. Not a security boundary: an organization of one's own grants nothing over
/// anyone else's, and can always be created from the portal.
/// </param>
public sealed record LoginSettings(
    LoginBranding Branding,
    LoginMethods Methods,
    bool AllowRegistration,
    IReadOnlyList<ProfileField> ProfileFields,
    bool AllowTenantCreation = true)
{
    public static readonly LoginSettings Default = new(
        new LoginBranding(null, null, null, null),
        new LoginMethods(Password: true, Passkey: true, Google: true, Microsoft: true),
        AllowRegistration: true,
        [],
        AllowTenantCreation: true);
}

/// <param name="Logo">data:image/(png|jpeg|webp|svg+xml);base64,… shown on top of the form instead of the Herensuge logo.</param>
/// <param name="PrimaryColor">#rrggbb of the main buttons.</param>
/// <param name="TermsUrl">Terms of service, linked from the registration form.</param>
/// <param name="PrivacyUrl">Privacy policy, linked from the registration form.</param>
/// <param name="HomeUrl">
/// Address of the application (OIDC client_uri), where the login pages send people when they are not in the middle of
/// signing in to it: after accepting an invitation, for instance.
/// </param>
public sealed record LoginBranding(string? Logo, string? PrimaryColor, string? TermsUrl, string? PrivacyUrl, string? HomeUrl = null);

/// <summary>
/// Ways to sign in offered on the page. Enforced when the code is issued: a session started with a method the client
/// does not accept has to sign in again (Google and Microsoft are also checked when the sign-in starts).
/// </summary>
public sealed record LoginMethods(bool Password, bool Passkey, bool Google, bool Microsoft);

/// <param name="Key">Name of the value in the user's profile and in the profile_fields claim ("company").</param>
/// <param name="Label">What the form shows ("Empresa").</param>
/// <param name="Type">One of <see cref="ProfileFieldTypes"/>.</param>
/// <param name="Options">Choices of a select field; empty for the other types.</param>
public sealed record ProfileField(string Key, string Label, string Type, bool Required, IReadOnlyList<string> Options)
{
    /// <summary>A required field the user has not filled in yet (a required checkbox must be "true").</summary>
    public bool IsMissingIn(IReadOnlyDictionary<string, string> values) =>
        Required
        && (!values.TryGetValue(Key, out string? value)
            || string.IsNullOrEmpty(value)
            || (Type == ProfileFieldTypes.Checkbox && value != "true"));
}

public static class ProfileFieldTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Date = "date";
    public const string Phone = "phone";
    public const string Select = "select";

    /// <summary>A yes/no consent ("Acepto recibir novedades"). Stored as "true" or "false"; required means "true".</summary>
    public const string Checkbox = "checkbox";

    public static readonly string[] All = [Text, Number, Date, Phone, Select, Checkbox];
}
