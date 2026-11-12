using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Herensuge.Modules.Oidc.Infrastructure.Keys;

/// <summary>
/// Loads the signing and encryption certificates from disk, creating self-signed ones on first start.
/// Rotate by replacing the files (keep the old signing key published until issued tokens expire).
/// </summary>
internal static class OidcCertificates
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(730);

    public static X509Certificate2 LoadOrCreate(string path, string? password, string subject, X509KeyUsageFlags usage, bool autoGenerate)
    {
        if (!File.Exists(path))
        {
            if (!autoGenerate)
            {
                throw new InvalidOperationException(
                    $"The OIDC certificate '{path}' does not exist. Provide it or set Oidc:Keys:AutoGenerate=true.");
            }

            Create(path, password, subject, usage);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(path, password, KeyStorageFlags);
    }

    private static X509KeyStorageFlags KeyStorageFlags =>
        OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;

    private static void Create(string path, string? password, string subject, X509KeyUsageFlags usage)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using RSA rsa = RSA.Create(3072);
        var request = new CertificateRequest(new X500DistinguishedName($"CN={subject}"), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 certificate = request.CreateSelfSigned(now.AddMinutes(-5), now + Validity);
        byte[] pfx = certificate.Export(X509ContentType.Pfx, password);

        try
        {
            // CreateNew: when several replicas start together only the first one writes the file.
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(pfx);
        }
        catch (IOException) when (File.Exists(path))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
