using Herensuge.Common.Infrastructure.Security;
using Herensuge.Modules.Identity.Application.Abstractions;
using Herensuge.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Herensuge.Modules.Identity.Infrastructure.Database.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        // Personal data is encrypted; the email is unique and searchable through its blind index.
        builder.Property(user => user.Email).IsEncrypted(EncryptedColumns.UserEmail);
        builder.Property<string>(IdentityDbContext.EmailIndex).HasMaxLength(64);
        builder.HasIndex(IdentityDbContext.EmailIndex).IsUnique();
        builder.Property(user => user.DisplayName).IsEncrypted(EncryptedColumns.UserDisplayName);
        builder.Property(user => user.PhoneNumber).IsEncrypted(EncryptedColumns.UserPhoneNumber);
        builder.Property(user => user.PasswordHash).HasMaxLength(256);
        builder.Property(user => user.TotpSecret).HasMaxLength(1024);
        builder.Property(user => user.PendingTotpSecret).HasMaxLength(1024);

        // Optimistic concurrency on the PostgreSQL xmin system column.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasMany(user => user.RecoveryCodes).WithOne().HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.ExternalLogins).WithOne().HasForeignKey(login => login.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.Passkeys).WithOne().HasForeignKey(passkey => passkey.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.KnownDevices).WithOne().HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.RecoveryCodes).AutoInclude();
        builder.Navigation(user => user.ExternalLogins).AutoInclude();
        builder.Navigation(user => user.Passkeys).AutoInclude();
        builder.Navigation(user => user.KnownDevices).AutoInclude();
    }
}

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("user_recovery_codes");
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Id).ValueGeneratedNever();
        builder.Property(code => code.CodeHash).HasMaxLength(64);
    }
}

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("user_external_logins");
        builder.HasKey(login => login.Id);
        builder.Property(login => login.Id).ValueGeneratedNever();
        builder.Property(login => login.Provider).HasMaxLength(50);
        builder.Property(login => login.Subject).HasMaxLength(255);
        builder.Property(login => login.Email).IsEncrypted(EncryptedColumns.ExternalLoginEmail);
        builder.HasIndex(login => new { login.Provider, login.Subject }).IsUnique();
        builder.HasIndex(login => new { login.UserId, login.Provider }).IsUnique();
    }
}

internal sealed class PasskeyCredentialConfiguration : IEntityTypeConfiguration<PasskeyCredential>
{
    public void Configure(EntityTypeBuilder<PasskeyCredential> builder)
    {
        builder.ToTable("user_passkeys");
        builder.HasKey(passkey => passkey.Id);
        builder.Property(passkey => passkey.Id).ValueGeneratedNever();
        builder.Property(passkey => passkey.Name).IsEncrypted(EncryptedColumns.PasskeyName);
        builder.HasIndex(passkey => passkey.CredentialId).IsUnique();
    }
}

internal sealed class KnownDeviceConfiguration : IEntityTypeConfiguration<KnownDevice>
{
    public void Configure(EntityTypeBuilder<KnownDevice> builder)
    {
        builder.ToTable("user_known_devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();
        builder.Property(device => device.DeviceIdHash).HasMaxLength(64);
        builder.HasIndex(device => new { device.UserId, device.DeviceIdHash }).IsUnique();
    }
}
