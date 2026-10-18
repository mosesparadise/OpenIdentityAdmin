using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Checks the real native HTTP factory rejects a correctly named but untrusted TLS certificate after trusted interoperability succeeds.</summary>
internal static class NativeClientTlsTrust
{
    private const string Subject = "CN=localhost";
    private const string Hostname = "localhost";
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    private const int KeyBits = 3072;
    private const int ValidityDays = 1;
    private const int NotBeforeMinutes = 1;

    /// <summary>Exercises a fresh real TLS connection with native trust settings; the HTTP delegate must never receive the request.</summary>
    internal static async Task RequireUntrustedAsync(IServiceProvider services)
    {
        using var certificate = Certificate();
        await using var untrusted = await NativeIssuerHttpsHost.StartAsync(certificate);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var context = new OpenIddictClientEvents.PrepareConfigurationRequestContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = Assert.Single(options.Registrations),
            Logger = NullLogger.Instance
        });
        await new OpenIddictClientSystemNetHttpHandlers.CreateHttpClient<OpenIddictClientEvents.PrepareConfigurationRequestContext>(
            services.GetRequiredService<IHttpClientFactory>()).HandleAsync(context);
        using var client = context.Transaction.GetHttpClient();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client!.GetAsync(untrusted.Issuer, TestContext.Current.CancellationToken));
        Assert.Equal(HttpRequestError.SecureConnectionError, error.HttpRequestError);
        await untrusted.DrainAsync();
        Assert.Equal(0, untrusted.Requests);
        Assert.Equal(0, untrusted.Failures);
    }

    /// <summary>Creates a synthetic valid localhost server certificate without adding its issuer to any trust store.</summary>
    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Hostname);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(ServerAuthentication) }, critical: true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-NotBeforeMinutes), DateTimeOffset.UtcNow.AddDays(ValidityDays));
    }
}
