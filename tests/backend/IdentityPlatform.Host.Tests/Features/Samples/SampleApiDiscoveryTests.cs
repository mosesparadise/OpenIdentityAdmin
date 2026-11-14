using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Sample.Api.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Requires actual native discovery and public-key HTTP retrieval before the ordinary API can accept a signed token.</summary>
public sealed class SampleApiDiscoveryTests
{
    private const string Development = "Development";
    private const string Enabled = "LocalDemo:Enabled";
    private const string True = "true";
    private const string Api = "https://orders.dev.localhost:17447/";
    private const string Orders = "orders";
    private const string Bearer = "Bearer";
    private const string Discovery = "/.well-known/openid-configuration";
    private const string Keys = "/connect/jwks";
    private const string InvalidHost = "https://wrong.invalid/";
    private const string Production = "--environment=Production";

    /// <summary>Native JSON/key extraction succeeds normally and rejects encoded discovery/JWKS before resource authority can be granted.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(Discovery)]
    [InlineData(Keys)]
    public async Task Given_NativePublicKeyDiscovery_When_EncodedOrValidResponsesArrive_Then_OnlyValidTrustEnablesOrders(string? hostile)
    {
        using var issuer = new SampleNativeIssuer { HostileResponse = hostile };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Enabled] = True });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(value => value.AdditionalHandlers.Add(issuer.Handler())));
        await using var app = SampleApplication.Create(builder);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(Api);
        using var request = new HttpRequestMessage(HttpMethod.Get, Orders);
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, issuer.ApiToken());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(hostile is null, response.StatusCode == HttpStatusCode.OK);
        Assert.True(issuer.Calls[hostile ?? Keys] > 0);
        Assert.Equal(0, issuer.TokenCalls);
        using var wrongHost = await client.GetAsync(InvalidHost + Orders, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, wrongHost.StatusCode);
    }

    /// <summary>Both composition roots reject production activation through their ordinary entry workflow with a finite nonzero result.</summary>
    [Fact]
    public async Task Given_ProductionEnvironment_When_SampleEntryRuns_Then_ActivationFailsBeforeAnyServiceStarts()
    {
        Assert.Equal(2, await SampleApplication.RunAsync([Production]));
        Assert.Equal(2, await IdentityPlatform.Sample.Client.Composition.SampleApplication.RunAsync([Production]));
    }
}
