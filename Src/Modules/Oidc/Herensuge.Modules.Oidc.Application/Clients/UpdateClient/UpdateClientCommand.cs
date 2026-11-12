namespace Herensuge.Modules.Oidc.Application.Clients.UpdateClient;

public sealed record UpdateClientCommand(
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes) : ICommand;
