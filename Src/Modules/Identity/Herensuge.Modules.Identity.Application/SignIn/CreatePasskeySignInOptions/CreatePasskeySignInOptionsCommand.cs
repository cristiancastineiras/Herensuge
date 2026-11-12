namespace Herensuge.Modules.Identity.Application.SignIn.CreatePasskeySignInOptions;

/// <summary>Returns the WebAuthn request options (JSON) for a usernameless passkey sign-in.</summary>
public sealed record CreatePasskeySignInOptionsCommand : ICommand<string>;
