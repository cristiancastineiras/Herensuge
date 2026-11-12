namespace Herensuge.Modules.Oidc.Application.Clients.DeleteClient;

/// <summary>Deletes the client together with its authorizations and tokens.</summary>
public sealed record DeleteClientCommand(string ClientId) : ICommand;
