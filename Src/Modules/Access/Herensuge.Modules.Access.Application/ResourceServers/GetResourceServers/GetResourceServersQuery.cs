namespace Herensuge.Modules.Access.Application.ResourceServers.GetResourceServers;

/// <summary>Registered applications (Platform).</summary>
public sealed record GetResourceServersQuery : IQuery<IReadOnlyList<ResourceServerResponse>>;

public sealed record ResourceServerResponse
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public bool EmbedPermissions { get; init; }

    public int ManifestVersion { get; init; }

    public int ActivePermissions { get; init; }

    public int DeprecatedPermissions { get; init; }

    public int RoleTemplates { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
