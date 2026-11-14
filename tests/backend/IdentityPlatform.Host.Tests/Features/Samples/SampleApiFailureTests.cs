using System.Net;
using IdentityPlatform.Sample.Api.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Requires finite request failure handling before and after actual response publication begins.</summary>
public sealed class SampleApiFailureTests
{
    private const string Development = "Development";
    private const string Enabled = "LocalDemo:Enabled";
    private const string True = "true";
    private const string Api = "https://orders.dev.localhost:17447/";
    private const string FailurePath = "/synthetic-failure";
    private const string PublicPrefix = "finite-prefix";
    private const string PrivateFailure = "private-error-canary";

    /// <summary>Unstarted failures produce no-store503; started failures abort rather than append private exception output.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_EndpointFailure_When_ResponseHasOrHasNotStarted_Then_ExceptionDetailsNeverBecomeOutput(bool started)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Enabled] = True });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        await using var app = SampleApplication.Create(builder);
        app.MapGet(FailurePath, async context =>
        {
            if (started)
            {
                await context.Response.WriteAsync(PublicPrefix, context.RequestAborted);
            }
            throw new InvalidOperationException(PrivateFailure);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(Api);
        try
        {
            using var response = await client.GetAsync(FailurePath, TestContext.Current.CancellationToken);
            Assert.False(started);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.DoesNotContain(PrivateFailure, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        catch (Exception error) when (started && error is HttpRequestException or OperationCanceledException or IOException) { }
    }
}
