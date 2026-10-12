using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Creates a short-lived loopback leaf signed by an unrelated, untrusted synthetic authority.</summary>
internal static class BrowserCertificate
{
    private const int KeyBits = 2048;
    private const int SerialBytes = 16;
    private const int AuthorityMinutes = 5;
    private const int LeafMinutes = 4;
    private const string LeafName = "CN=identityplatform-untrusted-test";
    private const string AuthorityName = "CN=identityplatform-untrusted-authority";
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    /// <summary>Returns an owned certificate; neither its authority nor its leaf is installed into a trust store.</summary>
    internal static X509Certificate2 Create()
    {
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest(LeafName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(ServerAuthentication) }, true));
        using var authorityKey = RSA.Create(KeyBits);
        var authorityRequest = new CertificateRequest(AuthorityName, authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        var now = DateTimeOffset.UtcNow;
        using var authority = authorityRequest.CreateSelfSigned(now.AddMinutes(-1), now.AddMinutes(AuthorityMinutes));
        using var issued = request.Create(authority, now.AddMinutes(-1), now.AddMinutes(LeafMinutes), RandomNumberGenerator.GetBytes(SerialBytes));
        return issued.CopyWithPrivateKey(key);
    }
}
