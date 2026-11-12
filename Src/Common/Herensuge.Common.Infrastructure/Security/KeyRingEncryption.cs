using System.Xml.Linq;
using Herensuge.Common.Application.Security;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace Herensuge.Common.Infrastructure.Security;

/// <summary>
/// Encrypts the ASP.NET Data Protection key ring (it protects cookies and TOTP secrets) with Security:EncryptionKeys
/// before it is stored next to the data, so a copy of the database cannot unprotect anything.
/// </summary>
public sealed class KeyRingXmlEncryptor(IDataEncryptor encryptor) : IXmlEncryptor
{
    internal const string Purpose = "common.data_protection_keys";

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        string ciphertext = encryptor.Encrypt(plaintextElement.ToString(SaveOptions.DisableFormatting), Purpose);
        return new EncryptedXmlInfo(new XElement("encryptedKey", ciphertext), typeof(KeyRingXmlDecryptor));
    }
}

/// <summary>Created by Data Protection from the type name stored in the key XML.</summary>
public sealed class KeyRingXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        return XElement.Parse(services.GetRequiredService<IDataEncryptor>().Decrypt(encryptedElement.Value, KeyRingXmlEncryptor.Purpose));
    }
}
