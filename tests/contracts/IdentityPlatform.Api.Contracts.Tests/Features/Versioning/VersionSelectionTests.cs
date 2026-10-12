using System.Net;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Versioning;

public sealed class VersionSelectionTests
{
    [Theory]
    [InlineData(false, "v1", HttpStatusCode.OK)]
    [InlineData(true, "v1", HttpStatusCode.OK)]
    [InlineData(false, "v2", HttpStatusCode.NotFound)]
    [InlineData(true, "v2", HttpStatusCode.NotFound)]
    [InlineData(false, "vbad", HttpStatusCode.NotFound)]
    [InlineData(true, "vbad", HttpStatusCode.NotFound)]
    [InlineData(false, "", HttpStatusCode.NotFound)]
    [InlineData(true, "", HttpStatusCode.NotFound)]
    public async Task Url_major_is_explicit_and_never_falls_back(bool browserSurface, string version, HttpStatusCode expected)
    {
        await using var host = await ContractHost.StartAsync(browserSurface);
        var prefix = browserSurface ? "/bff/api/" : "/api/";
        using var request = new HttpRequestMessage(HttpMethod.Get, prefix + (version.Length == 0 ? "" : version + "/") + "examples?api-version=1");
        request.Headers.Add("api-version", "1");
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal("1", Assert.Single(response.Headers.GetValues("api-supported-versions")));
            Assert.False(response.Headers.Contains("Sunset"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Query_and_header_cannot_override_the_supported_url_major(bool browserSurface)
    {
        await using var host = await ContractHost.StartAsync(browserSurface);
        using var request = new HttpRequestMessage(HttpMethod.Get, (browserSurface ? "/bff" : "") + "/api/v1/examples?api-version=99");
        request.Headers.Add("api-version", "99");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
    }
}
