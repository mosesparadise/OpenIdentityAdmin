using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Generates one disposable role and retains only secret parameter references plus optional public registration data.</summary>
internal sealed record DemoCertificate(IResourceBuilder<ParameterResource> Pfx, IResourceBuilder<ParameterResource> Password, string PublicKey)
{
    private const string SubjectPrefix = "CN=OpenIdentityAdmin disposable ";
    private const string RsaType = "RSA";
    private const string AssertionAlgorithm = "RS256";
    private const string SignatureUse = "sig";
    private const string ParameterPrefix = "demo-";
    private const string PfxSuffix = "-pfx";
    private const string PasswordSuffix = "-password";
    private const string MaterialPrefix = "LocalDemoMaterial__";
    private const string PfxVariableSuffix = "__Pfx";
    private const string PasswordVariableSuffix = "__Password";
    private const int ClockSkewMinutes = 5;
    private const int RsaBits = 3072;
    private const int PasswordBytes = 32;

    /// <summary>Exports a freshly generated encrypted role without files; private-key objects and mutable export buffers are disposed immediately.</summary>
    internal static DemoCertificate Create(IDistributedApplicationBuilder builder, string role, bool signing)
    {
        using var rsa = RSA.Create(RsaBits);
        var request = new CertificateRequest(SubjectPrefix + role, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(signing ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-ClockSkewMinutes), DateTimeOffset.UtcNow.AddDays(1));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(PasswordBytes));
        var bytes = certificate.Export(X509ContentType.Pkcs12, password);
        try
        {
            var encoded = Convert.ToBase64String(bytes);
            var key = rsa.ExportParameters(includePrivateParameters: false);
            var document = JsonSerializer.Serialize(new
            {
                kty = RsaType,
                kid = LocalDemoProfile.AssertionKeyId,
                alg = AssertionAlgorithm,
                use = SignatureUse,
                n = Base64Url.EncodeToString(key.Modulus!),
                e = Base64Url.EncodeToString(key.Exponent!)
            });
            return new(builder.AddParameter(ParameterPrefix + role.ToLowerInvariant() + PfxSuffix, () => encoded, secret: true),
                builder.AddParameter(ParameterPrefix + role.ToLowerInvariant() + PasswordSuffix, () => password, secret: true), document);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Sends encrypted material only to the owning container; masking does not conceal Docker metadata from local operators.</summary>
    internal void Apply(IResourceBuilder<ContainerResource> resource, string role) => resource
        .WithEnvironment(MaterialPrefix + role + PfxVariableSuffix, Pfx)
        .WithEnvironment(MaterialPrefix + role + PasswordVariableSuffix, Password);
}
