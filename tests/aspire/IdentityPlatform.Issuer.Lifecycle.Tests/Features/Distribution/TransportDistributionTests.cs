using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

[Collection("distribution")]
public sealed class TransportDistributionTests(DistributionFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Selected_socket_preserves_certificate_validation_and_canonical_authority(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        await using var untrusted = new TestRelyingParty { ClientSecret = environment.ClientSecret };
        await untrusted.StartAsync(TestContext.Current.CancellationToken, certificate);
        using (var client = new HttpClient(new NodeTransport(environment.Issuer, environment.RelyingParty.BaseUri,
            untrusted.BaseUri.Port, new CookieContainer(), redirects: false)))
        {
            var failure = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                using var response = await client.GetAsync(new Uri(environment.Issuer, "/alive"), TestContext.Current.CancellationToken);
            });
            Assert.IsType<AuthenticationException>(failure.InnerException);
        }
        // A valid selected transport recovers with the original canonical request authority.
        using var trusted = environment.NodeClient("auth-secondary");
        using var ready = await trusted.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var changedHost = new HttpRequestMessage(HttpMethod.Get, new Uri(environment.Issuer, "/health"));
        changedHost.Headers.Host = "outside.test";
        await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(async () =>
        {
            using var response = await trusted.SendAsync(changedHost, TestContext.Current.CancellationToken);
        });
        // The original connection pool is already warm; authority checks must still run.
        using var recovery = await trusted.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);

        await using var redirector = new TestRelyingParty { ClientSecret = environment.ClientSecret, Issuer = new Uri("https://outside.test/") };
        await redirector.StartAsync(TestContext.Current.CancellationToken);
        using var redirects = new HttpClient(new NodeTransport(environment.Issuer, redirector.BaseUri, untrusted.BaseUri.Port,
            new CookieContainer(), redirects: true));
        var flow = redirector.Prepare();
        await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(async () =>
        {
            using var response = await redirects.GetAsync(redirector.StartUri(flow), TestContext.Current.CancellationToken);
        });
        Assert.True(flow.Started, "The redirect guard control must first reach the trusted relying-party origin.");
    }
}
