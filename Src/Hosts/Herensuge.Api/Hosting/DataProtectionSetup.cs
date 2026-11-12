using System.Security.Cryptography.X509Certificates;
using Herensuge.Common.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Api.Hosting;

/// <summary>Data Protection key ring (cookies, TOTP secrets, OpenIddict client state) in the "common" schema.</summary>
public sealed class DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "common";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}

internal static class DataProtectionSetup
{
    /// <summary>
    /// Keys are shared by every instance through PostgreSQL. In production set
    /// DataProtection:CertificatePath so the keys are also encrypted at rest.
    /// </summary>
    public static IServiceCollection AddHerensugeDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<DataProtectionDbContext>(DataProtectionDbContext.Schema);

        IDataProtectionBuilder dataProtection = services.AddDataProtection()
            .SetApplicationName("herensuge-api")
            .PersistKeysToDbContext<DataProtectionDbContext>();

        string? certificatePath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            dataProtection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                configuration["DataProtection:CertificatePassword"],
                X509KeyStorageFlags.EphemeralKeySet));
        }

        return services;
    }
}
