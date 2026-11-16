namespace Herensuge.Modules.Access.Application.Catalog.GetAccessCatalog;

/// <summary>What an organization can grant: every application with its active permissions and its roles.</summary>
// shortcut: lists every permission, also those outside the organization's plan (they grant nothing, plan D5); filter by plan if organizations ask for it.
public sealed record GetAccessCatalogQuery : IQuery<IReadOnlyList<ApplicationCatalogResponse>>;

public sealed record ApplicationCatalogResponse(
    string Key,
    string Name,
    IReadOnlyList<PermissionResponse> Permissions,
    IReadOnlyList<RoleTemplateResponse> Roles);

public sealed record PermissionResponse
{
    public Guid Id { get; init; }

    public Guid ResourceServerId { get; init; }

    public string Key { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>none, mfa, recent_auth or trusted_network.</summary>
    public string Requires { get; init; } = string.Empty;

    /// <summary>Feature of the application the organization's plan must include; null: always.</summary>
    public string? FeatureKey { get; init; }

    /// <summary>owner, editor or viewer when it is also granted per object (plan D10).</summary>
    public string? Relation { get; init; }

    /// <summary>or / and, with a relation.</summary>
    public string? RelationMode { get; init; }
}

public sealed record RoleTemplateResponse
{
    public Guid Id { get; init; }

    public Guid ResourceServerId { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string[] PermissionKeys { get; init; } = [];

    /// <summary>Base roles (owner, admin, member) that have it without an assignment.</summary>
    public string[] GrantTo { get; init; } = [];
}
