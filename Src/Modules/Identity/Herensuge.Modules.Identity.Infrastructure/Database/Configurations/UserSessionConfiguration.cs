using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Tokens;
using Herensuge.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Herensuge.Modules.Identity.Infrastructure.Database.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.IpAddress).HasMaxLength(64);
        builder.Property(session => session.UserAgent).HasMaxLength(512);
        builder.Property(session => session.DeviceIdHash).HasMaxLength(64);
        builder.Property(session => session.RevocationReason).HasMaxLength(50);
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasIndex(session => session.UserId);

        // Same module: rows go away with the user (right to erasure).
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> builder)
    {
        builder.ToTable("one_time_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.Property(token => token.Target).HasMaxLength(Email.MaxLength);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.Purpose });

        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
