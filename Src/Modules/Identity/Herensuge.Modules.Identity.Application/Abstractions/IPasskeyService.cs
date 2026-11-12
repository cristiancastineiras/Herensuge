using Herensuge.Common.Domain;

namespace Herensuge.Modules.Identity.Application.Abstractions;

/// <summary>WebAuthn ceremonies. Challenges are kept server-side between the options and the verification.</summary>
public interface IPasskeyService
{
    Task<string> CreateRegistrationOptionsAsync(
        PasskeyUser user,
        IReadOnlyCollection<byte[]> existingCredentialIds,
        CancellationToken cancellationToken);

    Task<Result<NewPasskey>> VerifyRegistrationAsync(Guid userId, string attestationResponseJson, CancellationToken cancellationToken);

    Task<string> CreateAssertionOptionsAsync(CancellationToken cancellationToken);

    Result<byte[]> ReadCredentialId(string assertionResponseJson);

    Task<Result<PasskeyAssertion>> VerifyAssertionAsync(
        string assertionResponseJson,
        StoredPasskey storedPasskey,
        CancellationToken cancellationToken);
}

public sealed record PasskeyUser(Guid Id, string Email, string DisplayName);

public sealed record NewPasskey(byte[] CredentialId, byte[] PublicKey, long SignCount, Guid Aaguid);

public sealed record StoredPasskey(byte[] CredentialId, byte[] PublicKey, long SignCount, Guid UserId);

public sealed record PasskeyAssertion(byte[] CredentialId, long SignCount);
