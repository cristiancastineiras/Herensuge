using FluentValidation;

namespace Herensuge.Modules.Oidc.Application.Clients.UpdateClient;

internal sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RedirectUris).ValidRedirectUris().Must(uris => uris.Count > 0).WithMessage("At least one redirect URI is required.");
        RuleFor(command => command.PostLogoutRedirectUris).ValidRedirectUris();
        RuleFor(command => command.Scopes).ValidScopes();
    }
}
