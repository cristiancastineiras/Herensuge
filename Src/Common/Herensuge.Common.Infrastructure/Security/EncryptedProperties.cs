using Herensuge.Common.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Herensuge.Common.Infrastructure.Security;

/// <summary>
/// Transparent column encryption for EF Core: the entity keeps the plain value; the database only sees
/// AES-256-GCM ciphertext.
/// <code>
/// builder.Property(user => user.DisplayName).IsEncrypted("identity.users.display_name");
/// modelBuilder.UseDataEncryption(encryptor); // at the end of OnModelCreating
/// </code>
/// Encrypted columns cannot be searched, sorted or compared in SQL (each value has its own random nonce): look
/// them up through a blind index (<see cref="Herensuge.Common.Domain.ISecretHasher"/>) instead.
/// </summary>
public static class EncryptedProperties
{
    private const string PurposeAnnotation = "Herensuge:EncryptionPurpose";

    /// <param name="purpose">Never change it: it is authenticated with every value (schema.table.column).</param>
    public static PropertyBuilder<TProperty> IsEncrypted<TProperty>(this PropertyBuilder<TProperty> property, string purpose)
    {
        if (typeof(TProperty) != typeof(string))
        {
            throw new InvalidOperationException("Only string properties can be encrypted.");
        }

        return property.HasAnnotation(PurposeAnnotation, purpose).HasColumnType("text");
    }

    /// <summary>Adds the converters to every property marked with <see cref="IsEncrypted{TProperty}"/>.</summary>
    public static ModelBuilder UseDataEncryption(this ModelBuilder modelBuilder, IDataEncryptor encryptor)
    {
        foreach (IMutableProperty property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (property.FindAnnotation(PurposeAnnotation)?.Value is string purpose)
            {
                property.SetValueConverter(new ValueConverter<string, string>(
                    value => encryptor.Encrypt(value, purpose),
                    value => encryptor.Decrypt(value, purpose)));
            }
        }

        return modelBuilder;
    }
}
