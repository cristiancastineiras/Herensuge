using System.Text.RegularExpressions;
using FluentValidation;

namespace Herensuge.Modules.Oidc.Application.Clients.LoginPage;

public sealed record UpdateLoginSettingsCommand(string ClientId, LoginSettings Settings) : ICommand;

internal sealed class UpdateLoginSettingsCommandHandler(IClientRegistry clients) : ICommandHandler<UpdateLoginSettingsCommand>
{
    public Task<Result> Handle(UpdateLoginSettingsCommand command, CancellationToken cancellationToken) =>
        clients.UpdateLoginSettingsAsync(command.ClientId, command.Settings, cancellationToken);
}

internal sealed partial class UpdateLoginSettingsCommandValidator : AbstractValidator<UpdateLoginSettingsCommand>
{
    /// <summary>About 100 KB of image once decoded: the logo travels inline with the login page settings.</summary>
    public const int MaxLogoLength = 140_000;

    public const int MaxProfileFields = 15;
    public const int MaxOptions = 30;

    public UpdateLoginSettingsCommandValidator()
    {
        RuleFor(command => command.Settings).NotNull();
        RuleFor(command => command.Settings.Branding).NotNull().When(command => command.Settings is not null);
        RuleFor(command => command.Settings.Methods)
            .Must(methods => methods is not null && (methods.Password || methods.Passkey || methods.Google || methods.Microsoft))
            .When(command => command.Settings is not null)
            .WithMessage("At least one sign-in method must be enabled.");
        RuleFor(command => command.Settings.ProfileFields)
            .NotNull()
            .Must(fields => fields.Count <= MaxProfileFields).WithMessage($"At most {MaxProfileFields} fields.")
            .Must(fields => fields.Select(field => field?.Key).Distinct(StringComparer.Ordinal).Count() == fields.Count)
            .WithMessage("Field keys must be unique.")
            .When(command => command.Settings?.ProfileFields is not null);
        RuleForEach(command => command.Settings.ProfileFields)
            .NotNull()
            .SetValidator(new ProfileFieldValidator())
            .When(command => command.Settings?.ProfileFields is not null);

        When(command => command.Settings?.Branding is not null, () =>
        {
            RuleFor(command => command.Settings.Branding.Logo)
                .Must(logo => logo is null || (logo.Length <= MaxLogoLength && LogoDataUrl().IsMatch(logo)))
                .WithMessage("The logo must be a PNG, JPEG, WebP or SVG image of at most 100 KB.");
            RuleFor(command => command.Settings.Branding.PrimaryColor)
                .Must(color => color is null || HexColor().IsMatch(color))
                .WithMessage("The color must look like #1d4ed8.");
            RuleFor(command => command.Settings.Branding.TermsUrl).Must(BeNullOrHttpsUrl).WithMessage("Use an absolute HTTPS URL.");
            RuleFor(command => command.Settings.Branding.PrivacyUrl).Must(BeNullOrHttpsUrl).WithMessage("Use an absolute HTTPS URL.");
            RuleFor(command => command.Settings.Branding.HomeUrl).Must(BeNullOrHttpsUrl).WithMessage("Use an absolute HTTPS URL.");
        });
    }

    private static bool BeNullOrHttpsUrl(string? value) =>
        value is null
        || (value.Length <= 500
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)));

    [GeneratedRegex("^data:image/(png|jpeg|webp|svg\\+xml);base64,[A-Za-z0-9+/]+={0,2}$")]
    private static partial Regex LogoDataUrl();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    private sealed partial class ProfileFieldValidator : AbstractValidator<ProfileField>
    {
        public ProfileFieldValidator()
        {
            RuleFor(field => field.Key).NotEmpty().Matches(Key()).WithMessage("Keys use lowercase letters, digits and underscores (\"company_size\").");
            RuleFor(field => field.Label).NotEmpty().MaximumLength(80);
            RuleFor(field => field.Type).Must(type => ProfileFieldTypes.All.Contains(type)).WithMessage($"Type must be one of: {string.Join(", ", ProfileFieldTypes.All)}.");
            RuleFor(field => field.Options).NotNull();
            RuleFor(field => field.Options)
                .Must(options => options.Count is > 0 and <= MaxOptions
                    && options.All(option => !string.IsNullOrWhiteSpace(option) && option.Length <= 80)
                    && options.Distinct(StringComparer.Ordinal).Count() == options.Count)
                .When(field => field.Type == ProfileFieldTypes.Select && field.Options is not null)
                .WithMessage($"A select field needs between 1 and {MaxOptions} different options.");
            RuleFor(field => field.Options)
                .Empty()
                .When(field => field.Type != ProfileFieldTypes.Select && field.Options is not null)
                .WithMessage("Only select fields have options.");
        }

        [GeneratedRegex("^[a-z][a-z0-9_]{0,39}$")]
        private static partial Regex Key();
    }
}
