using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Holds genuine accepted retirement responses to prove original browser authority controls publication independently of durable API commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementPublicationTests(BootstrapFixture fixture)
{
    private const string Control = "control";
    private const string Generation = "generation";
    private const string Expiry = "expiry";
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string Retirement = "/retirement";
    private const string Grants = "/grant-revocations/";
    private const string Separator = "/";
    private const string GuidFormat = "D";
    private const string IdField = "id";
    private const int WatchdogSeconds = 30;

    /// <summary>A held positive control and two real browser-state changes isolate publication from already committed retirement on both providers.</summary>
    [Theory]
    [InlineData("PostgreSql", Control, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Generation, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Expiry, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Control, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Generation, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Expiry, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedRetirement_When_BrowserAuthorityIsRecheckedBeforePublication_Then_OutputFollowsOriginalBoundsAndCommittedHistorySurvives(string provider, string boundary)
    {
        var probe = new GrantTransportProbe();
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: services => services.AddSingleton<IStartupFilter>(probe));
        using var created = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var id = (await ApplicationHttpTests.CreatedAsync(created)).Application.Id;
        var path = ApplicationHttpFixture.ApiCollection + Separator + id;
        await probe.BindAsync(test);
        probe.Path = path + Retirement;
        probe.Body = Body;
        probe.Version = created.Headers.ETag!.Tag;
        probe.Key = Guid.NewGuid().ToString(GuidFormat);
        probe.Fault = GrantTransportFaults.Hold;
        var proof = await test.TokenAsync();
        if (boundary == Expiry)
        {
            await RetirementPublicationEvidence.ShortenAsync(test);
        }
        RetirementPublicationEvidence.Commit? committed = null;
        var pending = test.CatalogueAsync(HttpMethod.Post, ApplicationHttpTests.Path(id) + Retirement, Body, probe.Version, direct: false, csrf: proof, key: probe.Key);
        using var response = await CrossAsync(pending, probe, async () =>
        {
            committed = await RetirementPublicationEvidence.ObserveAsync(test, id, probe.Version, probe.Key);
            if (boundary != Control)
            {
                await RetirementPublicationEvidence.ChangeAsync(test, boundary == Generation);
            }
        });
        Assert.NotNull(committed);
        await RequireResponseAsync(response, committed, boundary == Control, ApplicationHttpTests.Path(id));
        await RequireRecoveryAsync(test, probe, committed);
    }

    /// <summary>Requires one upstream attempt and unchanged durable state before and after an explicit authorized status read.</summary>
    private static async Task RequireRecoveryAsync(ApplicationHttpFixture test, GrantTransportProbe probe, RetirementPublicationEvidence.Commit committed)
    {
        var id = committed.Receipt.ApplicationId;
        Assert.Equal(1, probe.Requests);
        Assert.Equal(1, probe.Responses);
        Assert.Equal(0, probe.Redirects);
        Assert.False(probe.Canceled.Task.IsCompleted);
        Assert.Equal(committed.Snapshot, await RetirementPublicationEvidence.SnapshotAsync(test, id));
        using var status = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + id + Grants + committed.Receipt.GrantJobId.ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(committed.Receipt.GrantJobId.ToString(GuidFormat), json.RootElement.GetProperty(IdField).GetString());
        Assert.Equal(committed.Snapshot, await RetirementPublicationEvidence.SnapshotAsync(test, id));
    }

    /// <summary>Requires exact safe acceptance for the held control and only a sanitized problem without successful identifiers or headers after authority loss.</summary>
    private static async Task RequireResponseAsync(HttpResponseMessage response, RetirementPublicationEvidence.Commit committed, bool control, string path)
    {
        await GrantTransportTests.SafeAsync(response);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (control)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var accepted = JsonSerializer.Deserialize<ApplicationRetirementAcceptance>(body, JsonSerializerOptions.Web)!;
            Assert.Equal(committed.Receipt.ApplicationId, accepted.ApplicationId);
            Assert.Equal(committed.Receipt.RequestKey.ToString(GuidFormat), accepted.RequestKey);
            Assert.Equal(committed.Receipt.GrantJobId.ToString(GuidFormat), accepted.Job.Id);
            Assert.Equal(accepted.Job.Version, response.Headers.ETag!.Tag);
            Assert.Equal(path + Grants + accepted.Job.Id, response.Headers.Location!.OriginalString);
            return;
        }
        await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(committed.Receipt.ApplicationId, body, StringComparison.Ordinal);
        Assert.DoesNotContain(committed.Receipt.RequestKey.ToString(GuidFormat), body, StringComparison.Ordinal);
        Assert.DoesNotContain(committed.Receipt.GrantJobId.ToString(GuidFormat), body, StringComparison.Ordinal);
    }

    /// <summary>Requires an observed committed-response barrier and always releases and joins both real requests before provider teardown.</summary>
    private static async Task<HttpResponseMessage> CrossAsync(Task<HttpResponseMessage> pending, GrantTransportProbe probe, Func<Task> change)
    {
        var token = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(WatchdogSeconds);
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(probe.Entered.Task, pending).WaitAsync(timeout, token);
            Assert.True(probe.Entered.Task.IsCompletedSuccessfully);
            Assert.False(pending.IsCompleted);
            await change();
            Assert.False(pending.IsCompleted);
        }
        catch (Exception error) { failures.Add(error); }
        finally { probe.Release.TrySetResult(); }
        HttpResponseMessage? response = null;
        try
        {
            response = await pending.WaitAsync(timeout, token);
            await probe.Completed.Task.WaitAsync(timeout, token);
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
