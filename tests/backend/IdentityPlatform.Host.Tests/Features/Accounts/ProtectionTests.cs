using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Exercises the administrator-only shared pipeline rather than inferring protection from registration.</summary>
public sealed class ProtectionTests
{
    private const string Alive = "/alive";
    private const string SetCookie = "Set-Cookie";
    private const int RequestBudget = 600;
    private const int OversizeBytes = 16_385;
    private const string PilotLogin = "/account/login";
    private const string CspHeader = "Content-Security-Policy";
    private const int WindowProbeSeconds = 30;
    private const int TimeoutMinimumSeconds = 9;
    private const int TimeoutMaximumSeconds = 15;
    private const int TrafficFamilies = 2;
    private const int BodyLimit = 16_384;

    /// <summary>Proves one shared fixed-window budget in administrator-only and combined composition.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AdministratorPipeline_When_WindowIsExhausted_Then_DenialIsSafeAndLivenessRemainsAvailable(bool pilot)
    {
        const string WindowFailure = "Admission must be checked inside the original minute.";
        var entered = 0;
        await using var host = await AccountHost.StartAsync(pilot, true, app => app.MapGet(AccountHost.Probe, () =>
        {
            Interlocked.Increment(ref entered);
            return Results.NoContent();
        }));
        var elapsed = Stopwatch.StartNew();
        for (var index = 0; index < RequestBudget; index++)
        {
            var usePilot = pilot && index % TrafficFamilies == 0;
            using var accepted = await host.Client.GetAsync(usePilot ? PilotLogin : AccountHost.Probe, TestContext.Current.CancellationToken);
            Assert.Equal(usePilot ? HttpStatusCode.BadRequest : HttpStatusCode.NoContent, accepted.StatusCode);
        }
        using var denied = await host.Client.GetAsync(AccountHost.Probe, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.Equal(pilot ? RequestBudget / TrafficFamilies : RequestBudget, entered);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(WindowProbeSeconds), WindowFailure);
        Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
        Assert.Equal("Too many sign-in requests. Wait a minute and try again.", await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var alive = await host.Client.GetAsync(Alive, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    /// <summary>Enforces the actual cooperative ten-second timeout without pilot activation.</summary>
    [Fact]
    public async Task Given_AdministratorOnly_When_RequestNeverCompletes_Then_TimeoutCancelsAndDrainsIt()
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await AccountHost.StartAsync(false, true, app => app.MapGet(AccountHost.Probe, async (HttpContext context) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            finally
            {
                stopped.TrySetResult();
            }
        }));
        var elapsed = Stopwatch.StartNew();
        using var response = await host.Client.GetAsync(AccountHost.Probe, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(stopped.Task.IsCompletedSuccessfully);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(TimeoutMinimumSeconds) && elapsed.Elapsed < TimeSpan.FromSeconds(TimeoutMaximumSeconds));
        Assert.False(response.Headers.Contains(SetCookie));
    }

    /// <summary>Rejects oversized bodies before handlers, and verifies native form/antiforgery bounds.</summary>
    [Fact]
    public async Task Given_AdministratorOnly_When_BodyIsOversized_Then_ItNeverReachesTheEndpoint()
    {
        var entered = false;
        await using var host = await AccountHost.StartAsync(false, true, app => app.MapPost(AccountHost.Probe, () =>
        {
            entered = true;
            return Results.NoContent();
        }));
        using var content = new ByteArrayContent(new byte[OversizeBytes]);
        using var response = await host.Client.PostAsync(AccountHost.Probe, content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.False(entered);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(response.Headers.Contains(CspHeader));
        var form = host.Services.GetRequiredService<IOptions<FormOptions>>().Value;
        Assert.Equal(BodyLimit, form.MultipartBodyLengthLimit);
        Assert.Equal(16, form.ValueCountLimit);
        var anti = host.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        Assert.Equal(CookieSecurePolicy.Always, anti.Cookie.SecurePolicy);
        Assert.True(anti.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, anti.Cookie.SameSite);
        Assert.Null(anti.Cookie.Domain);
    }
}
