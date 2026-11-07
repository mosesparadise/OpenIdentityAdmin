using System.Diagnostics;
using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantTransportContext;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Holds actual valid TLS responses after API work to isolate original BFF elapsed, browser-generation and expiry publication boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantTransportBoundaryTests(BootstrapFixture fixture)
{
    private const string Deadline = "deadline";
    private const string Generation = "generation";
    private const string Expiry = "expiry";
    private const string ElapsedReport = "grant-bff-cancellation-elapsed-ms={0:F3}";
    private const int MaximumSeconds = 12;
    private const int WatchdogSeconds = 30;

    /// <summary>Withholds an otherwise valid response at the deadline boundary on all four closed routes.</summary>
    [Theory]
    [InlineData("PostgreSql", Create, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Continue, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Exact, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Recent, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Create, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Continue, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Exact, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Recent, Traits = ["Provider", "SqlServer"])]
    public Task Given_ValidApiResult_When_OriginalElapsedBudgetEnds_Then_ResultIsWithheldAndDurableWorkRemains(string provider, string shape) =>
        VerifyAsync(provider, shape, Deadline);

    /// <summary>Withholds an otherwise valid response at the generation boundary on all four closed routes.</summary>
    [Theory]
    [InlineData("PostgreSql", Create, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Continue, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Exact, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Recent, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Create, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Continue, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Exact, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Recent, Traits = ["Provider", "SqlServer"])]
    public Task Given_ValidApiResult_When_DurableBrowserGenerationChanges_Then_ResultIsWithheldAndDurableWorkRemains(string provider, string shape) =>
        VerifyAsync(provider, shape, Generation);

    /// <summary>Withholds an otherwise valid response at the expiry boundary on all four closed routes.</summary>
    [Theory]
    [InlineData("PostgreSql", Create, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Continue, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Exact, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Recent, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Create, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Continue, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Exact, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Recent, Traits = ["Provider", "SqlServer"])]
    public Task Given_ValidApiResult_When_OriginalBrowserExpiryIsReached_Then_ResultIsWithheldAndDurableWorkRemains(string provider, string shape) =>
        VerifyAsync(provider, shape, Expiry);

    /// <summary>Preserves exactly committed work and joins the single upstream attempt before explicit authorized recovery.</summary>
    private async Task VerifyAsync(string provider, string shape, string boundary)
    {
        await using var test = await CreateAsync(fixture, provider, shape);
        if (boundary == Expiry)
        {
            await test.ChangeBrowserAsync(generation: false);
        }
        var before = await test.SnapshotAsync();
        var now = test.Test.Now;
        test.Probe.Fault = GrantTransportFaults.Hold;
        var started = Stopwatch.GetTimestamp();
        using var response = await CrossBoundaryAsync(test, boundary);
        await ApplicationHttpTests.ProblemAsync(response, boundary == Deadline ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Unauthorized);
        await GrantTransportTests.SafeAsync(response);
        Assert.Equal(1, test.Probe.Requests);
        Assert.Equal(1, test.Probe.Responses);
        Assert.Equal(0, test.Probe.Redirects);
        if (boundary == Deadline)
        {
            Assert.Equal(now, test.Test.Now);
            TestContext.Current.TestOutputHelper?.WriteLine(ElapsedReport, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(MaximumSeconds));
            Assert.True(test.Probe.Canceled.Task.IsCompletedSuccessfully);
        }
        if (!test.Mutation)
        {
            Assert.Equal(before, await test.SnapshotAsync());
        }
        await test.RequireRecoveryAsync();
    }

    /// <summary>Always releases and joins the real request and upstream middleware, including assertion failures, before the fixture can dispose its hosts.</summary>
    private static async Task<HttpResponseMessage> CrossBoundaryAsync(GrantTransportContext test, string boundary)
    {
        var token = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(WatchdogSeconds);
        var pending = test.SendAsync();
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(test.Probe.Entered.Task, pending).WaitAsync(timeout, token);
            Assert.True(test.Probe.Entered.Task.IsCompletedSuccessfully);
            Assert.False(pending.IsCompleted);
            if (boundary == Generation)
            {
                await test.ChangeBrowserAsync(generation: true);
            }
            if (boundary == Expiry)
            {
                await test.ExtendAndExpireOriginalAsync();
            }
            if (boundary == Deadline)
            {
                await test.Probe.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(MaximumSeconds), token);
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally { test.Probe.Release.TrySetResult(); }
        HttpResponseMessage? response = null;
        try
        {
            response = await pending.WaitAsync(timeout, token);
            await test.Probe.Completed.Task.WaitAsync(timeout, token);
        }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count == 0)
        {
            return response!;
        }
        response?.Dispose();
        throw new AggregateException(failures);
    }
}
