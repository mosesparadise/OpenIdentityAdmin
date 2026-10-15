using System.Diagnostics;
using System.Net;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class OperationsTests(IssuerFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Audit_insert_failure_rolls_back_password_failure_and_session_creation(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        var login = await TestLogin.BeginAsync(environment, browser, prompt: "login");
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = (await users.FindByNameAsync(environment.Username))!;
        var failures = user.AccessFailedCount;
        var sessions = await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken);
        var audits = await database.Set<SecurityAuditIntent>().CountAsync(TestContext.Current.CancellationToken);
        var deny = provider == "PostgreSql" ? "REVOKE INSERT ON platform.\"SecurityAuditIntent\" FROM platform_runtime" : "DENY INSERT ON OBJECT::[platform].[SecurityAuditIntent] TO [platform_runtime]";
        var restore = provider == "PostgreSql" ? "GRANT INSERT ON platform.\"SecurityAuditIntent\" TO platform_runtime" : "REVOKE INSERT ON OBJECT::[platform].[SecurityAuditIntent] FROM [platform_runtime]";
        try
        {
            await database.Database.ExecuteSqlRawAsync(deny, TestContext.Current.CancellationToken);
            using var wrong = await login.SubmitAsync(environment, browser, password: "incorrect-synthetic-password");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, wrong.StatusCode);
            await database.Entry(user).ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(failures, user.AccessFailedCount);
            using var correct = await login.SubmitAsync(environment, browser);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, correct.StatusCode);
            Assert.Equal(sessions, await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(audits, await database.Set<SecurityAuditIntent>().CountAsync(TestContext.Current.CancellationToken));
            Assert.False(await database.Set<LocalIdentitySession>().Where(value => value.Id == original.ExpectedSessionId)
                .Select(value => value.Revoked).SingleAsync(TestContext.Current.CancellationToken), "Failed audit must not revoke the previous session.");
            var interaction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
            Assert.Null(interaction.CompletedSessionId);
            Assert.False(interaction.Consumed);
            Assert.True(string.IsNullOrEmpty(login.Flow.Code), "A denied flow unexpectedly issued a code.");
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await database.Database.ExecuteSqlRawAsync(restore, restoration.Token);
            }
            catch (Exception) { environment.Invalidate(); }
        }
        Assert.False(environment.Invalidated, "Audit permission restoration failed.");
        var reused = await environment.AuthorizeAsync(browser, exchange: true, prompt: "none");
        Assert.True(reused.Validated && reused.ExpectedSessionId == original.ExpectedSessionId, "Audit failure must preserve the usable original session.");
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true, prompt: "login")).Validated);
    }

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
            request.Headers.ExpectContinue = true;
            using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
            SafeAssertions.ContainsNoSensitiveMaterial(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new string('x', 100));
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
        SafeAssertions.ContainsNoSensitiveMaterial(body, flow.Code);
        Assert.True(body == "Sign-in is temporarily unavailable. Return to your application and try again.", "Unexpected outage response.");
        Assert.True(Guid.TryParseExact(response.Headers.GetValues("X-Identity-Incident").Single(), "N", out var incident) && incident != Guid.Empty,
            "A storage failure must expose only an independent incident identifier for support.");
        using var ready = await browser.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        using var live = await browser.GetAsync(new Uri(environment.Issuer, "/alive"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }
}
