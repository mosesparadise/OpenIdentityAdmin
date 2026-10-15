using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Loads only bounded password-encrypted native PKCS#12 material; callers retain role/validity policy and ownership.</summary>
internal static class ProtectedCertificate
{
    /// <summary>Returns one ephemeral private certificate and disposes partial loads before propagating a fixed failure.</summary>
    internal static X509Certificate2 Load(string? path, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ProtectionMaterialException();
        }
        var bytes = PrivateStorage.ReadBoundedFile(PrivateStorage.RequireCredentialFile(path));
        X509Certificate2? certificate = null;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.EphemeralKeySet,
                new Pkcs12LoaderLimits { MaxCertificates = 1, MaxKeys = 1 });
            RequireProtectedPrivateKey(bytes, password);
            return certificate;
        }
        catch { certificate?.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Requires password integrity and exactly one shrouded private key after native work limits have been applied.</summary>
    private static void RequireProtectedPrivateKey(byte[] bytes, string password)
    {
        var info = Pkcs12Info.Decode(bytes, out var consumed, skipCopy: true);
        if (consumed != bytes.Length || info.IntegrityMode != Pkcs12IntegrityMode.Password || !info.VerifyMac(password))
        {
            throw new ProtectionMaterialException();
        }
        var protectedKeys = 0;
        foreach (var contents in info.AuthenticatedSafe)
        {
            if (contents.ConfidentialityMode == Pkcs12ConfidentialityMode.Password)
            {
                contents.Decrypt(password);
            }
            else if (contents.ConfidentialityMode != Pkcs12ConfidentialityMode.None)
            {
                throw new ProtectionMaterialException();
            }
            foreach (var bag in contents.GetBags())
            {
                if (bag is Pkcs12ShroudedKeyBag)
                {
                    protectedKeys++;
                }
                else if (bag is not Pkcs12CertBag)
                {
                    throw new ProtectionMaterialException();
                }
            }
        }
        if (protectedKeys != 1)
        {
            throw new ProtectionMaterialException();
        }
    }
}
