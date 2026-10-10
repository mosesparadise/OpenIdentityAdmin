using System.Diagnostics;
using System.Net;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class OperationsTests(IssuerFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Both_known_length_and_chunked_oversized_forms_fail_safely(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        foreach (var chunked in new[] { false, true })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(environment.Issuer, "/account/login"));
            request.Content = new StringContent("Password=" + new string('x', 17_000), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Headers.TransferEncodingChunked = chunked;
            using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
            Assert.DoesNotContain(new string('x', 100), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Stopped_database_denies_a_valid_exchange_within_budget_without_liveness_failure(string provider)
    {
        // Fault injection owns its entire environment; it cannot contaminate shared happy-path state.
        await using var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        var application = environment.Application;
        var model = application.Services.GetRequiredService<DistributedApplicationModel>();
        var commands = application.Services.GetRequiredService<ResourceCommandService>();
        await commands.ExecuteCommandAsync(model.Resources.Single(value => value.Name == "database"), KnownResourceCommands.StopCommand, TestContext.Current.CancellationToken);
        await application.ResourceNotifications.WaitForResourceAsync("database", KnownResourceStates.Exited, TestContext.Current.CancellationToken);
        var elapsed = Stopwatch.StartNew();
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        elapsed.Stop();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(11), "Cooperative outage budget plus scheduling tolerance was exceeded.");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Sign-in is temporarily unavailable. Return to your application and try again.", body);
        Assert.DoesNotContain(flow.Code, body, StringComparison.Ordinal);
        using var ready = await browser.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        using var live = await browser.GetAsync(new Uri(environment.Issuer, "/alive"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }
}
