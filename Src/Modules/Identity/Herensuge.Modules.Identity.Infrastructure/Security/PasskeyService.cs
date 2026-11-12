using System.Buffers.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Domain.Users;
using Microsoft.Extensions.Caching.Distributed;

namespace Herensuge.Modules.Identity.Infrastructure.Security;

public sealed class PasskeyOptions
{
    public const string SectionName = "Identity:Passkeys";

    /// <summary>Registrable domain shared by the IdP and the portal (localhost in development).</summary>
    public string RelyingPartyId { get; set; } = "localhost";

    public string RelyingPartyName { get; set; } = "Herensuge";

    /// <summary>Exact origins allowed to run WebAuthn ceremonies (IdP and portal).</summary>
    public string[] Origins { get; set; } = ["https://localhost:5001", "https://localhost:5002"];
}

/// <summary>
/// WebAuthn with fido2-net-lib. Options (and their challenge) are kept in the distributed cache for
/// five minutes and consumed on first use.
/// </summary>
internal sealed class PasskeyService(IFido2 fido2, IDistributedCache cache, IUserRepository users) : IPasskeyService
{
    private static readonly DistributedCacheEntryOptions ChallengeLifetime = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    public async Task<string> CreateRegistrationOptionsAsync(
        PasskeyUser user,
        IReadOnlyCollection<byte[]> existingCredentialIds,
        CancellationToken cancellationToken)
    {
        CredentialCreateOptions options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.DisplayName },
            ExcludeCredentials = [.. existingCredentialIds.Select(id => new PublicKeyCredentialDescriptor(id))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        string json = options.ToJson();
        await cache.SetStringAsync(RegistrationKey(user.Id), json, ChallengeLifetime, cancellationToken);
        return json;
    }

    public async Task<Result<NewPasskey>> VerifyRegistrationAsync(Guid userId, string attestationResponseJson, CancellationToken cancellationToken)
    {
        string? optionsJson = await cache.GetStringAsync(RegistrationKey(userId), cancellationToken);
        if (optionsJson is null)
        {
            return UserErrors.PasskeyVerificationFailed;
        }

        await cache.RemoveAsync(RegistrationKey(userId), cancellationToken);

        try
        {
            AuthenticatorAttestationRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson);
            if (response is null)
            {
                return UserErrors.PasskeyVerificationFailed;
            }

            RegisteredPublicKeyCredential credential = await fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = response,
                    OriginalOptions = CredentialCreateOptions.FromJson(optionsJson),
                    IsCredentialIdUniqueToUserCallback = async (args, token) =>
                        !await users.PasskeyCredentialExistsAsync(args.CredentialId, token),
                },
                cancellationToken);

            return new NewPasskey(credential.Id, credential.PublicKey, credential.SignCount, credential.AaGuid);
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or ArgumentException or FormatException)
        {
            return UserErrors.PasskeyVerificationFailed;
        }
    }

    public async Task<string> CreateAssertionOptionsAsync(CancellationToken cancellationToken)
    {
        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });

        string json = options.ToJson();
        await cache.SetStringAsync(AssertionKey(Base64Url.EncodeToString(options.Challenge)), json, ChallengeLifetime, cancellationToken);
        return json;
    }

    public Result<byte[]> ReadCredentialId(string assertionResponseJson)
    {
        try
        {
            AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson);
            return response?.RawId is { Length: > 0 } rawId ? rawId : UserErrors.PasskeyVerificationFailed;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return UserErrors.PasskeyVerificationFailed;
        }
    }

    public async Task<Result<PasskeyAssertion>> VerifyAssertionAsync(
        string assertionResponseJson,
        StoredPasskey storedPasskey,
        CancellationToken cancellationToken)
    {
        try
        {
            AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson);
            if (response?.Response is null)
            {
                return UserErrors.PasskeyVerificationFailed;
            }

            string key = AssertionKey(ReadChallenge(response.Response.ClientDataJson));
            string? optionsJson = await cache.GetStringAsync(key, cancellationToken);
            if (optionsJson is null)
            {
                return UserErrors.PasskeyVerificationFailed;
            }

            await cache.RemoveAsync(key, cancellationToken);
            byte[] userHandle = storedPasskey.UserId.ToByteArray();

            VerifyAssertionResult result = await fido2.MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = response,
                    OriginalOptions = AssertionOptions.FromJson(optionsJson),
                    StoredPublicKey = storedPasskey.PublicKey,
                    StoredSignatureCounter = (uint)storedPasskey.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                        args.UserHandle.AsSpan().SequenceEqual(userHandle)
                        && args.CredentialId.AsSpan().SequenceEqual(storedPasskey.CredentialId)),
                },
                cancellationToken);

            return new PasskeyAssertion(result.CredentialId, result.SignCount);
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or ArgumentException or FormatException or KeyNotFoundException)
        {
            return UserErrors.PasskeyVerificationFailed;
        }
    }

    private static string ReadChallenge(byte[] clientDataJson)
    {
        using JsonDocument document = JsonDocument.Parse(clientDataJson);
        return document.RootElement.GetProperty("challenge").GetString() ?? string.Empty;
    }

    private static string RegistrationKey(Guid userId) => $"passkeys:registration:{userId:N}";

    private static string AssertionKey(string challenge) => $"passkeys:assertion:{challenge}";
}
