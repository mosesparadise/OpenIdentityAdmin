using System.Net;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

public sealed class DocumentationPolicyTests
{
    [Fact]
    public async Task Scalar_nonce_is_per_response_and_arbitrary_document_selectors_are_denied()
    {
        await using var host = await ContractHost.StartAsync(true);
        using var first = await host.Client.GetAsync("/docs/scalar/", TestContext.Current.CancellationToken);
        using var second = await host.Client.GetAsync("/docs/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var policy = Assert.Single(first.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("'nonce-", policy, StringComparison.Ordinal);
        Assert.NotEqual(policy, Assert.Single(second.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Equal("no-store", first.Headers.CacheControl?.ToString());
        foreach (var path in new[] { "/docs/scalar/unknown", "/docs/scalar/unknown.js" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        }
    }

    [Theory]
    [InlineData("/docs/assets/theme.css", "text/css")]
    [InlineData("/docs/assets/swagger.js", "text/javascript")]
    [InlineData("/docs/swagger/swagger-ui-bundle.js", "text/javascript")]
    [InlineData("/docs/scalar/scalar.js", "text/javascript")]
    [InlineData("/docs/scalar/scalar.aspnetcore.js", "text/javascript")]
    [InlineData("/docs/scalar/favicon.svg", "image/svg+xml")]
    public async Task Renderer_assets_are_local_and_retain_security_headers(string path, string type)
    {
        await using var host = await ContractHost.StartAsync(true);
        using var response = await host.Client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(type, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
