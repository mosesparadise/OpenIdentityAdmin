using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Supplies independent credentials for probes that inspect native registration and request selection.</summary>
internal static class RegistrationTestKeys
{
    // Match the pinned OpenIddict client's and server's ephemeral RSA defaults.
    private const int KeySize = 4096;
    private static readonly Lazy<byte[]> SigningTemplate = new(GenerateTemplate);
    private static readonly Lazy<byte[]> EncryptionTemplate = new(GenerateTemplate);

    /// <summary>Registers separate encryption and signing credentials in the server probe.</summary>
    internal static OpenIddictServerBuilder AddRegistrationTestKeys(this OpenIddictServerBuilder builder) =>
        builder.AddEncryptionCredentials(EncryptionCredentials()).AddSigningCredentials(SigningCredentials());

    /// <summary>Registers separate encryption and signing credentials in the client probe.</summary>
    internal static OpenIddictClientBuilder AddRegistrationTestKeys(this OpenIddictClientBuilder builder) =>
        builder.AddEncryptionCredentials(EncryptionCredentials()).AddSigningCredentials(SigningCredentials());

    /// <summary>Generates one process-local synthetic template for a single key role.</summary>
    private static byte[] GenerateTemplate()
    {
        using var rsa = RSA.Create(KeySize);
        return rsa.ExportPkcs8PrivateKey();
    }

    /// <summary>Gives the probe its own parameter arrays and releases the temporary native key handle.</summary>
    private static RsaSecurityKey Import(byte[] template)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(template, out _);
        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true));
    }

    /// <summary>Uses the native ephemeral RSA-OAEP and content-encryption algorithms.</summary>
    private static EncryptingCredentials EncryptionCredentials() =>
        new(Import(EncryptionTemplate.Value), SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512);

    /// <summary>Uses the native ephemeral RS256 signing algorithm.</summary>
    private static SigningCredentials SigningCredentials() =>
        new(Import(SigningTemplate.Value), SecurityAlgorithms.RsaSha256);
}
