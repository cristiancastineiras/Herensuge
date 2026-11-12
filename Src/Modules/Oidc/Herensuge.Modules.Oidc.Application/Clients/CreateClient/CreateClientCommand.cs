namespace Herensuge.Modules.Oidc.Application.Clients.CreateClient;

public sealed record CreateClientCommand(
    string ClientId,
    string DisplayName,
    bool IsConfidential,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes) : ICommand<ClientCreatedResponse>;

/// <param name="ClientSecret">Shown once; only its hash is stored.</param>
public sealed record ClientCreatedResponse(string ClientId, string? ClientSecret);
