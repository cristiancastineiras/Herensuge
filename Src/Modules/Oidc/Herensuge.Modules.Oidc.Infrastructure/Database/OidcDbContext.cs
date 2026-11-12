using Herensuge.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Herensuge.Modules.Oidc.Infrastructure.Database;

/// <summary>OpenIddict applications, authorizations, scopes and tokens, plus the module inbox.</summary>
public sealed class OidcDbContext(DbContextOptions<OidcDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(OidcModule.Schema);
        modelBuilder.UseOpenIddict<Guid>();
        modelBuilder.ApplyMessagingConfiguration(includeOutbox: false);
    }
}
