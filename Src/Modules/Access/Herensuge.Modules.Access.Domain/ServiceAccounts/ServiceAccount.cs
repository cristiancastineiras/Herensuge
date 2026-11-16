using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.ServiceAccounts;

/// <summary>
/// A machine of an organization (plan D7): its OIDC client (client_credentials) lives in Oidc; without this aggregate
/// the client cannot get a token. It holds roles like a member (subject "client").
/// </summary>
public sealed class ServiceAccount : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxPerTenant = 50;

    private ServiceAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ClientId { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Checked before the OIDC client is created.</summary>
    public static Result ValidateName(string? name) =>
        (name?.Trim().Length ?? 0) is 0 or > MaxNameLength ? AccessErrors.InvalidServiceAccountName : Result.Success();

    public static Result<ServiceAccount> Create(Guid tenantId, string name, string clientId, Guid createdByUserId, DateTime utcNow)
    {
        Result valid = ValidateName(name);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var account = new ServiceAccount
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            ClientId = clientId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
        account.Raise(new ServiceAccountCreatedDomainEvent(tenantId, account.Id, account.Name, clientId));
        return account;
    }

    public void SecretRotated() => Raise(new ServiceAccountSecretRotatedDomainEvent(TenantId, Id, ClientId));

    /// <summary>The caller removes its assignments in the same unit of work, then deletes the OIDC client.</summary>
    public void Delete() => Raise(new ServiceAccountDeletedDomainEvent(TenantId, Id, Name, ClientId));
}

public sealed record ServiceAccountCreatedDomainEvent(Guid TenantId, Guid ServiceAccountId, string Name, string ClientId) : DomainEvent;

public sealed record ServiceAccountSecretRotatedDomainEvent(Guid TenantId, Guid ServiceAccountId, string ClientId) : DomainEvent;

public sealed record ServiceAccountDeletedDomainEvent(Guid TenantId, Guid ServiceAccountId, string Name, string ClientId) : DomainEvent;
