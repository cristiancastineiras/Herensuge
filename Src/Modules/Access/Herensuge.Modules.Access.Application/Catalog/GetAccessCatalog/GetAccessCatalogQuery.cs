namespace Herensuge.Modules.Access.Application.Catalog.GetAccessCatalog;

/// <summary>What an organization can grant: every application with its active permissions and its roles.</summary>
// shortcut: every application is listed for every organization; filter by contracted features with plans (plan, phase 2).
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

    /// <summary>none, mfa or recent_auth.</summary>
    public string Requires { get; init; } = string.Empty;
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
