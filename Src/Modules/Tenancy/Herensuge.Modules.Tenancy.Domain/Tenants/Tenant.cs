using System.Globalization;
using System.Text;
using Herensuge.Common.Domain;
using Herensuge.Modules.Tenancy.Domain.Tenants.Events;

namespace Herensuge.Modules.Tenancy.Domain.Tenants;

/// <summary>An organization (customer of the SaaS).</summary>
public sealed class Tenant : AggregateRoot
{
    public const int MaxNameLength = 100;

    private Tenant()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool RequireMfa { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Tenant Create(string name, TenantSlug slug, Guid createdByUserId, DateTime utcNow)
    {
        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = NormalizeName(name),
            Slug = slug.Value,
            IsActive = true,
            CreatedAtUtc = utcNow,
        };

        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id, tenant.Name, tenant.Slug, createdByUserId));
        return tenant;
    }

    public void Update(string name, bool requireMfa)
    {
        string normalized = NormalizeName(name);
        if (normalized == Name && requireMfa == RequireMfa)
        {
            return;
        }

        Name = normalized;
        RequireMfa = requireMfa;
        Raise(new TenantUpdatedDomainEvent(Id, Name, RequireMfa));
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        Raise(new TenantStatusChangedDomainEvent(Id, isActive));
    }

    private static string NormalizeName(string name)
    {
        string trimmed = name.Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }
}

/// <summary>URL-friendly unique identifier of an organization: lowercase letters, digits and dashes.</summary>
public sealed record TenantSlug
{
    public const int MaxLength = 50;

    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static TenantSlug FromName(string name)
    {
        string decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        string slug = builder.ToString().Trim('-');
        if (slug.Length > MaxLength - 5)
        {
            slug = slug[..(MaxLength - 5)].Trim('-');
        }

        return new TenantSlug(slug.Length >= 3 ? slug : "org");
    }

    /// <summary>Same slug with a short random suffix, used when the plain one is taken.</summary>
    public TenantSlug WithSuffix(string suffix) => new($"{Value}-{suffix}");

    public override string ToString() => Value;
}

public static class TenantErrors
{
    public static readonly Error NotFound = Error.NotFound("Tenants.NotFound", "The organization does not exist.");

    public static readonly Error Inactive = Error.Forbidden("Tenants.Inactive", "The organization is deactivated.");
}
