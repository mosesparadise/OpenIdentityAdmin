using System.Net;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class DistributionHarnessTests
{
    [Fact]
    public void Listener_requires_owned_loopback_socket_and_collapses_ipv4_ipv6_port()
    {
        var rows = new[]
        {
            "0: 0100007F:1234 00000000:0000 0A 0 0 0 0 0 111",
            "1: 00000000000000000000000001000000:1234 0:0000 0A 0 0 0 0 0 112",
            "2: 00000000:9999 00000000:0000 0A 0 0 0 0 0 999",
            "3: 0100007F:8888 00000000:0000 01 0 0 0 0 0 111"
        };
        Assert.Equal(0x1234, ProcessListener.RequirePort(rows, new HashSet<string>(StringComparer.Ordinal) { "111", "112" }));
        Assert.Throws<Xunit.Sdk.TrueException>(() => ProcessListener.RequirePort(rows, new HashSet<string>(StringComparer.Ordinal) { "999" }));
        Assert.Throws<Xunit.Sdk.TrueException>(() => ProcessListener.RequirePort(rows, new HashSet<string>(StringComparer.Ordinal) { "absent" }));
        Assert.Throws<Xunit.Sdk.TrueException>(() => ProcessListener.RequirePort(rows.Append("4: 0100007F:7777 0:0 0A 0 0 0 0 0 111"),
            new HashSet<string>(StringComparer.Ordinal) { "111" }));
        Assert.Throws<Xunit.Sdk.TrueException>(() => ProcessListener.RequirePort(["malformed"], new HashSet<string>(StringComparer.Ordinal) { "111" }));
    }

    [Theory]
    [InlineData("http://localhost:443/")]
    [InlineData("https://example.test/")]
    [InlineData("https://user@localhost/")]
    [InlineData("https://localhost/?unexpected=value")]
    public void Transport_rejects_unsuitable_origin_before_any_connection(string address) =>
        Assert.Throws<Xunit.Sdk.TrueException>(() => NodeTransport.RequireOrigin(new Uri(address)));

    [Fact]
    public async Task Each_request_rejects_unknown_authority_plaintext_and_host_overrides()
    {
        var issuer = new Uri("https://localhost:43001/");
        var relyingParty = new Uri("https://localhost:43002/");
        using var client = new HttpClient(new NodeTransport(issuer, relyingParty, 43003, new CookieContainer(), redirects: false));
        foreach (var uri in new[] { "https://localhost:43004/", "http://localhost:43001/", "https://outside.test/", "https://user@localhost:43001/" })
        {
            await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(async () =>
            {
                using var response = await client.GetAsync(uri, TestContext.Current.CancellationToken);
            });
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, issuer);
        request.Headers.Host = "outside.test";
        await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(async () =>
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        });
    }
}
