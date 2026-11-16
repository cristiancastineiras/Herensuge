using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Access.Application.Services;
using Herensuge.Modules.Access.Domain.ServiceAccounts;
using Herensuge.Modules.Oidc.Contracts;

namespace Herensuge.Modules.Access.Application.ServiceAccounts;

public sealed record CreateServiceAccountCommand(Guid TenantId, Guid ActorUserId, string Name) : ICommand<ServiceAccountCreatedResponse>;

/// <summary>The secret is shown only here and after a rotation.</summary>
public sealed record ServiceAccountCreatedResponse(Guid Id, string ClientId, string ClientSecret);

public sealed record RotateServiceAccountSecretCommand(Guid TenantId, Guid ActorUserId, Guid ServiceAccountId) : ICommand<ServiceAccountSecretResponse>;

public sealed record ServiceAccountSecretResponse(string ClientSecret);

public sealed record DeleteServiceAccountCommand(Guid TenantId, Guid ActorUserId, Guid ServiceAccountId) : ICommand;

public sealed record GetServiceAccountsQuery(Guid TenantId) : IQuery<IReadOnlyList<ServiceAccountResponse>>;

public sealed record ServiceAccountResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public int Roles { get; init; }
}

/// <summary>Plan D7: Oidc owns the client and Access the aggregate; the order keeps a client from outliving its account.</summary>
internal sealed class ServiceAccountCommandHandlers(
    TenantAdministration administration,
    IServiceAccountRepository serviceAccounts,
    IRoleAssignmentRepository assignments,
    IOidcModuleApi oidc,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) :
    ICommandHandler<CreateServiceAccountCommand, ServiceAccountCreatedResponse>,
    ICommandHandler<RotateServiceAccountSecretCommand, ServiceAccountSecretResponse>,
    ICommandHandler<DeleteServiceAccountCommand>
{
    public async Task<Result<ServiceAccountCreatedResponse>> Handle(CreateServiceAccountCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await RequireAsync(command.TenantId, command.ActorUserId, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        if (await serviceAccounts.CountAsync(command.TenantId, cancellationToken) >= ServiceAccount.MaxPerTenant)
        {
            return AccessErrors.TooManyServiceAccounts;
        }

        Result valid = ServiceAccount.ValidateName(command.Name);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        ServiceClient client = await oidc.CreateServiceClientAsync(command.TenantId, command.Name.Trim(), cancellationToken);
        ServiceAccount account = ServiceAccount.Create(
            command.TenantId, command.Name, client.ClientId, command.ActorUserId, timeProvider.GetUtcNow().UtcDateTime).Value;
        serviceAccounts.Add(account);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // A client without its aggregate cannot get tokens, but it should not stay around either.
            await oidc.DeleteServiceClientAsync(client.ClientId, CancellationToken.None);
            throw;
        }

        return new ServiceAccountCreatedResponse(account.Id, client.ClientId, client.ClientSecret);
    }

    public async Task<Result<ServiceAccountSecretResponse>> Handle(RotateServiceAccountSecretCommand command, CancellationToken cancellationToken)
    {
        Result<ServiceAccount> account = await FindAsync(command.TenantId, command.ActorUserId, command.ServiceAccountId, cancellationToken);
        if (account.IsFailure)
        {
            return account.Error;
        }

        if (await oidc.RotateServiceClientSecretAsync(account.Value.ClientId, cancellationToken) is not { } secret)
        {
            return AccessErrors.ServiceAccountNotFound;
        }

        account.Value.SecretRotated();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceAccountSecretResponse(secret);
    }

    public async Task<Result> Handle(DeleteServiceAccountCommand command, CancellationToken cancellationToken)
    {
        Result<ServiceAccount> account = await FindAsync(command.TenantId, command.ActorUserId, command.ServiceAccountId, cancellationToken);
        if (account.IsFailure)
        {
            return account.Error;
        }

        foreach (RoleAssignment assignment in await assignments.ListAsync(command.TenantId, [account.Value.Id], cancellationToken))
        {
            assignment.Revoke();
            assignments.Remove(assignment);
        }

        account.Value.Delete();
        serviceAccounts.Remove(account.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await oidc.DeleteServiceClientAsync(account.Value.ClientId, cancellationToken);
        return Result.Success();
    }

    private Task<Result> RequireAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken) =>
        administration.RequirePermissionAsync(tenantId, actorUserId, TenantAdministration.ManageServiceAccounts, cancellationToken);

    private async Task<Result<ServiceAccount>> FindAsync(Guid tenantId, Guid actorUserId, Guid serviceAccountId, CancellationToken cancellationToken)
    {
        Result allowed = await RequireAsync(tenantId, actorUserId, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        ServiceAccount? account = await serviceAccounts.GetAsync(tenantId, serviceAccountId, cancellationToken);
        return account is null ? AccessErrors.ServiceAccountNotFound : account;
    }
}

internal sealed class GetServiceAccountsQueryHandler(IDbConnectionFactory dbConnectionFactory, TimeProvider timeProvider)
    : IQueryHandler<GetServiceAccountsQuery, IReadOnlyList<ServiceAccountResponse>>
{
    private const string Sql =
        """
        SELECT s.id, s.name, s.client_id, s.created_at_utc,
               (SELECT count(*) FROM access.role_assignments a
                WHERE a.subject_id = s.id AND (a.expires_at_utc IS NULL OR a.expires_at_utc > @Now))::int AS roles
        FROM access.service_accounts s
        WHERE s.tenant_id = @TenantId
        ORDER BY s.name
        """;

    public async Task<Result<IReadOnlyList<ServiceAccountResponse>>> Handle(GetServiceAccountsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<ServiceAccountResponse> rows = await connection.QueryAsync<ServiceAccountResponse>(new CommandDefinition(
            Sql,
            new { query.TenantId, Now = timeProvider.GetUtcNow().UtcDateTime },
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<ServiceAccountResponse>>([.. rows]);
    }
}
