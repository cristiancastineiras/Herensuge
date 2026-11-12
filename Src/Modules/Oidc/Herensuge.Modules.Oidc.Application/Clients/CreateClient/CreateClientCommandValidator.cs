using FluentValidation;

namespace Herensuge.Modules.Oidc.Application.Clients.CreateClient;

internal sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(command => command.ClientId).ValidClientId();
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RedirectUris).ValidRedirectUris().Must(uris => uris.Count > 0).WithMessage("At least one redirect URI is required.");
        RuleFor(command => command.PostLogoutRedirectUris).ValidRedirectUris();
        RuleFor(command => command.Scopes).ValidScopes();
    }
}
