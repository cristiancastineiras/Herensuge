namespace Herensuge.Modules.Identity.Application;

public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    /// <summary>The user who verifies this email (or signs up with it through a provider that verifies it) becomes platform admin.</summary>
    public string? BootstrapPlatformAdminEmail { get; set; }

    internal bool IsBootstrapAdmin(string email) =>
        !string.IsNullOrWhiteSpace(BootstrapPlatformAdminEmail)
        && string.Equals(BootstrapPlatformAdminEmail.Trim(), email, StringComparison.OrdinalIgnoreCase);
}
