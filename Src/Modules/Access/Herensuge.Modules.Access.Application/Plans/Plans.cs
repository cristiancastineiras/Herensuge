using System.Data.Common;
using Dapper;
using Herensuge.Common.Application.Data;
using Herensuge.Modules.Access.Domain.Plans;

namespace Herensuge.Modules.Access.Application.Plans;

// Platform: plans are sets of application features (plan D5). The endpoints require a platform administrator.

public sealed record CreatePlanCommand(string Key, string Name, IReadOnlyList<string> Features) : ICommand;

public sealed record UpdatePlanCommand(string Key, string Name, IReadOnlyList<string> Features) : ICommand;

public sealed record SetDefaultPlanCommand(string Key) : ICommand;

public sealed record DeletePlanCommand(string Key) : ICommand;

/// <param name="PlanKey">Null: back to the default plan.</param>
public sealed record SetTenantPlanCommand(Guid TenantId, string? PlanKey) : ICommand;

public sealed record GetPlansQuery : IQuery<IReadOnlyList<PlanResponse>>;

public sealed record PlanResponse
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    /// <summary>"app/feature".</summary>
    public string[] Features { get; init; } = [];

    /// <summary>Organizations with this plan assigned (the default one also covers those without any).</summary>
    public int Tenants { get; init; }
}

public sealed record GetTenantPlansQuery : IQuery<IReadOnlyList<TenantPlanResponse>>;

public sealed record TenantPlanResponse
{
    public Guid TenantId { get; init; }

    public string PlanKey { get; init; } = string.Empty;
}

/// <summary>Organization: its plan (read only) and the features it includes, by application.</summary>
public sealed record GetTenantPlanQuery(Guid TenantId) : IQuery<TenantPlanDetailsResponse>;

/// <param name="Plan">Null when the installation has no plans: everything is included.</param>
public sealed record TenantPlanDetailsResponse(string? PlanKey, string? Plan, bool IsDefault, IReadOnlyList<PlanApplicationResponse> Applications);

public sealed record PlanApplicationResponse(string Key, string Name, IReadOnlyList<PlanFeatureResponse> Features);

public sealed record PlanFeatureResponse(string Key, string Name, bool Included);

internal sealed class PlanCommandHandlers(IPlanRepository plans, IResourceServerRepository servers, IUnitOfWork unitOfWork, TimeProvider timeProvider) :
    ICommandHandler<CreatePlanCommand>,
    ICommandHandler<UpdatePlanCommand>,
    ICommandHandler<SetDefaultPlanCommand>,
    ICommandHandler<DeletePlanCommand>,
    ICommandHandler<SetTenantPlanCommand>
{
    public async Task<Result> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> all = await plans.ListAsync(cancellationToken);
        if (all.Any(plan => plan.Key == command.Key))
        {
            return AccessErrors.PlanKeyInUse;
        }

        Result known = await EnsureFeaturesExistAsync(command.Features, cancellationToken);
        if (known.IsFailure)
        {
            return known;
        }

        // The first plan is the default one (plan D5).
        Result<Plan> plan = Plan.Create(command.Key, command.Name, command.Features ?? [], isDefault: all.Count == 0, timeProvider.GetUtcNow().UtcDateTime);
        if (plan.IsFailure)
        {
            return plan.Error;
        }

        plans.Add(plan.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(UpdatePlanCommand command, CancellationToken cancellationToken)
    {
        if (await plans.GetByKeyAsync(command.Key, cancellationToken) is not { } plan)
        {
            return AccessErrors.PlanNotFound;
        }

        Result known = await EnsureFeaturesExistAsync(command.Features, cancellationToken);
        if (known.IsFailure)
        {
            return known;
        }

        Result updated = plan.Update(command.Name, command.Features ?? []);
        if (updated.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<Result> Handle(SetDefaultPlanCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> all = await plans.ListAsync(cancellationToken);
        if (!all.Any(plan => plan.Key == command.Key))
        {
            return AccessErrors.PlanNotFound;
        }

        foreach (Plan plan in all)
        {
            plan.SetDefault(plan.Key == command.Key);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(DeletePlanCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> all = await plans.ListAsync(cancellationToken);
        if (all.FirstOrDefault(plan => plan.Key == command.Key) is not { } plan)
        {
            return AccessErrors.PlanNotFound;
        }

        if (plan.IsDefault && all.Count > 1)
        {
            return AccessErrors.DefaultPlanRequired;
        }

        // Organizations with this plan go back to the default one (database cascade on tenant_plans).
        plan.Delete();
        plans.Remove(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(SetTenantPlanCommand command, CancellationToken cancellationToken)
    {
        TenantPlan? current = await plans.GetTenantPlanAsync(command.TenantId, cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (command.PlanKey is null)
        {
            if (current is not null)
            {
                current.Remove();
                plans.Remove(current);
            }
        }
        else if (await plans.GetByKeyAsync(command.PlanKey, cancellationToken) is not { } plan)
        {
            return AccessErrors.PlanNotFound;
        }
        else if (current is null)
        {
            plans.Add(TenantPlan.Assign(command.TenantId, plan, now));
        }
        else
        {
            current.Change(plan, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> EnsureFeaturesExistAsync(IReadOnlyList<string>? features, CancellationToken cancellationToken)
    {
        HashSet<string> known = [.. (await servers.ListAsync(cancellationToken)).SelectMany(server => server.Features.Select(feature => $"{server.Key}/{feature.Key}"))];
        return (features ?? []).All(known.Contains) ? Result.Success() : AccessErrors.UnknownFeature;
    }
}

internal sealed class PlanQueryHandlers(IDbConnectionFactory dbConnectionFactory, IPlanRepository plans, IResourceServerRepository servers) :
    IQueryHandler<GetPlansQuery, IReadOnlyList<PlanResponse>>,
    IQueryHandler<GetTenantPlansQuery, IReadOnlyList<TenantPlanResponse>>,
    IQueryHandler<GetTenantPlanQuery, TenantPlanDetailsResponse>
{
    public async Task<Result<IReadOnlyList<PlanResponse>>> Handle(GetPlansQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<PlanResponse> rows = await connection.QueryAsync<PlanResponse>(new CommandDefinition(
            """
            SELECT p.key, p.name, p.is_default, p.features,
                   (SELECT count(*) FROM access.tenant_plans t WHERE t.plan_id = p.id)::int AS tenants
            FROM access.plans p
            ORDER BY p.name
            """,
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<PlanResponse>>([.. rows]);
    }

    public async Task<Result<IReadOnlyList<TenantPlanResponse>>> Handle(GetTenantPlansQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<TenantPlanResponse> rows = await connection.QueryAsync<TenantPlanResponse>(new CommandDefinition(
            "SELECT t.tenant_id, p.key AS plan_key FROM access.tenant_plans t JOIN access.plans p ON p.id = t.plan_id",
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<TenantPlanResponse>>([.. rows]);
    }

    public async Task<Result<TenantPlanDetailsResponse>> Handle(GetTenantPlanQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> all = await plans.ListAsync(cancellationToken);
        TenantPlan? assigned = all.Count == 0 ? null : await plans.GetTenantPlanAsync(query.TenantId, cancellationToken);
        Plan? plan = all.FirstOrDefault(candidate => candidate.Id == assigned?.PlanId) ?? all.FirstOrDefault(candidate => candidate.IsDefault);

        PlanApplicationResponse[] applications =
        [
            .. (await servers.ListAsync(cancellationToken))
                .Where(server => server.Features.Count > 0)
                .OrderBy(server => server.Name, StringComparer.Ordinal)
                .Select(server => new PlanApplicationResponse(
                    server.Key,
                    server.Name,
                    [.. server.Features.OrderBy(feature => feature.Name, StringComparer.Ordinal)
                        .Select(feature => new PlanFeatureResponse(feature.Key, feature.Name, plan is null || plan.Includes(server.Key, feature.Key)))])),
        ];
        return new TenantPlanDetailsResponse(plan?.Key, plan?.Name, plan is not null && assigned is null, applications);
    }
}

/// <summary>Platform: every feature of every application, to build plans.</summary>
public sealed record GetFeaturesQuery : IQuery<IReadOnlyList<FeatureResponse>>;

public sealed record FeatureResponse
{
    public string ResourceServer { get; init; } = string.Empty;

    public string ResourceServerName { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

internal sealed class GetFeaturesQueryHandler(IDbConnectionFactory dbConnectionFactory) : IQueryHandler<GetFeaturesQuery, IReadOnlyList<FeatureResponse>>
{
    public async Task<Result<IReadOnlyList<FeatureResponse>>> Handle(GetFeaturesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        IEnumerable<FeatureResponse> rows = await connection.QueryAsync<FeatureResponse>(new CommandDefinition(
            """
            SELECT s.key AS resource_server, s.name AS resource_server_name, f.key, f.name
            FROM access.features f
            JOIN access.resource_servers s ON s.id = f.resource_server_id
            ORDER BY s.name, f.name
            """,
            cancellationToken: cancellationToken));
        return Result.Success<IReadOnlyList<FeatureResponse>>([.. rows]);
    }
}
