namespace Herensuge.Modules.Oidc.Application.Clients.RotateClientSecret;

/// <summary>Generates a new secret; the previous one stops working immediately.</summary>
public sealed record RotateClientSecretCommand(string ClientId) : ICommand<string>;
