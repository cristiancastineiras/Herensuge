using System.Text.RegularExpressions;
using Herensuge.Common.Domain;

namespace Herensuge.Modules.Access.Domain.Plans;

/// <summary>
/// A set of features of the applications (plan D5). If plans exist, exactly one is the default: organizations without
/// a <see cref="TenantPlan"/> have it. Without plans, everything is included.
/// </summary>
public sealed partial class Plan : AggregateRoot
{
    public const int MaxFeatures = 500;

    private Plan()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>"pro": lowercase letters, digits and dashes.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    /// <summary>"facturas/approvals": application key and feature key.</summary>
    public string[] Features { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Plan> Create(string key, string name, IReadOnlyCollection<string> features, bool isDefault, DateTime utcNow)
    {
        if (!KeyPattern().IsMatch(key ?? string.Empty) || key!.Length > 50)
        {
            return AccessErrors.InvalidPlan("key must use lowercase letters, digits and dashes (max 50).");
        }

        var plan = new Plan { Id = Guid.CreateVersion7(), Key = key, IsDefault = isDefault, CreatedAtUtc = utcNow };
        Result set = plan.Set(name, features);
        if (set.IsFailure)
        {
            return set.Error;
        }

        plan.Raise(new PlanChangedDomainEvent(plan.Key, plan.Name, plan.Features, Deleted: false));
        return plan;
    }

    public Result Update(string name, IReadOnlyCollection<string> features)
    {
        Result set = Set(name, features);
        if (set.IsSuccess)
        {
            Raise(new PlanChangedDomainEvent(Key, Name, Features, Deleted: false));
        }

        return set;
    }

    /// <summary>The caller keeps exactly one default (plan D5).</summary>
    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void Delete() => Raise(new PlanChangedDomainEvent(Key, Name, Features, Deleted: true));

    public bool Includes(string resourceServer, string feature) => Features.Contains($"{resourceServer}/{feature}", StringComparer.Ordinal);

    private Result Set(string name, IReadOnlyCollection<string> features)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 100)
        {
            return AccessErrors.InvalidPlan("name is required (max 100 characters).");
        }

        if (features.Count > MaxFeatures || features.Any(feature => !FeaturePattern().IsMatch(feature ?? string.Empty)))
        {
            return AccessErrors.InvalidPlan($"features are \"app/feature\" keys (at most {MaxFeatures}).");
        }

        Name = trimmed;
        Features = [.. features.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return Result.Success();
    }

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*/[a-z][a-z0-9-]*$")]
    private static partial Regex FeaturePattern();
}

/// <summary>The plan of one organization; without a row, the default plan applies.</summary>
public sealed class TenantPlan : AggregateRoot
{
    private TenantPlan()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }

    public static TenantPlan Assign(Guid tenantId, Plan plan, DateTime utcNow)
    {
        var tenantPlan = new TenantPlan { TenantId = tenantId, PlanId = plan.Id, AssignedAtUtc = utcNow };
        tenantPlan.Raise(new TenantPlanChangedDomainEvent(tenantId, plan.Key));
        return tenantPlan;
    }

    public void Change(Plan plan, DateTime utcNow)
    {
        if (PlanId != plan.Id)
        {
            PlanId = plan.Id;
            AssignedAtUtc = utcNow;
            Raise(new TenantPlanChangedDomainEvent(TenantId, plan.Key));
        }
    }

    /// <summary>Back to the default plan.</summary>
    public void Remove() => Raise(new TenantPlanChangedDomainEvent(TenantId, PlanKey: null));
}

public sealed record PlanChangedDomainEvent(string Key, string Name, IReadOnlyList<string> Features, bool Deleted) : DomainEvent;

/// <param name="PlanKey">Null: the organization has the default plan.</param>
public sealed record TenantPlanChangedDomainEvent(Guid TenantId, string? PlanKey) : DomainEvent;
