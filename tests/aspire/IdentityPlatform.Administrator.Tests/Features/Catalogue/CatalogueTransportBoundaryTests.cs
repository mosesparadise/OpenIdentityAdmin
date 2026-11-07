using System.Diagnostics;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Withholds genuine catalogue API responses across the original browser authority and elapsed publication boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueTransportBoundaryTests(BootstrapFixture fixture)
{
    private const string Deadline = "deadline";
    private const string Generation = "generation";
    private const string Expiry = "expiry";
    private const string Api = "/api/v1/audiences";
    private const string Browser = "/bff";
    private const string Separator = "/";
    private const string Session = "/bff/session";
    private const string Authenticated = "authenticated";
    private const string Create = "{\"resource\":\"orders-api\",\"displayName\":\"Orders\",\"owner\":\"Operations\",\"enabled\":true}";
    private const int WatchdogSeconds = 30;
    private const int MaximumSeconds = 12;
    private const int ExtendedSeconds = 60;
    private const string GuidFormat = "D";
    private const string Admitted = "CatalogueDeletionAdmitted";

    /// <summary>One native successful GET cannot publish after cancellation, generation replacement or its original deadline, even when persisted expiry is extended.</summary>
    [Theory]
    [InlineData("PostgreSql", Deadline, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Generation, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Expiry, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Deadline, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Generation, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Expiry, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ValidCatalogueApiResult_When_OriginalBrowserBoundaryEnds_Then_OneSendCannotPublishOrChangeCatalogue(string provider, string boundary)
    {
        var probe = new GrantTransportProbe();
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: services => services.AddSingleton<IStartupFilter>(probe));
        using var created = await test.CatalogueAsync(HttpMethod.Post, Api, Create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var audience = (await CatalogueHttpEvidence.AudienceAsync(created))!;
        await probe.BindAsync(test);
        probe.Path = Api + Separator + audience.Id;
        probe.Fault = GrantTransportFaults.Hold;
        if (boundary == Expiry)
        {
            await ChangeBrowserAsync(test, Generation, shorten: true);
        }
        var before = await CatalogueHttpEvidence.SnapshotAsync(test);
        var now = test.Now;
        var started = Stopwatch.GetTimestamp();
        using var response = await CrossBoundaryAsync(test, probe, boundary);
        await ApplicationHttpTests.ProblemAsync(response, boundary == Deadline ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Unauthorized);
        await GrantTransportTests.SafeAsync(response);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain(audience.Id, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(1, probe.Requests);
        Assert.Equal(1, probe.Responses);
        Assert.Equal(0, probe.Redirects);
        Assert.Equal(before, await CatalogueHttpEvidence.SnapshotAsync(test));
        if (boundary == Deadline)
        {
            Assert.Equal(now, test.Now);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(MaximumSeconds));
            Assert.True(probe.Canceled.Task.IsCompletedSuccessfully);
        }
    }

    /// <summary>A genuinely committed DELETE with a withheld response is recovered explicitly without another approval, audit or authority extension.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedCatalogueDeletion_When_BrowserGenerationChangesBeforePublication_Then_ExplicitRecoveryPreservesSingleOriginalApproval(string provider)
    {
        var probe = new GrantTransportProbe();
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: services => services.AddSingleton<IStartupFilter>(probe));
        using var created = await test.CatalogueAsync(HttpMethod.Post, Api, Create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var audience = (await CatalogueHttpEvidence.AudienceAsync(created))!;
        var proof = await CatalogueHttpTests.ProofAsync(test);
        await probe.BindAsync(test);
        probe.Path = Api + Separator + audience.Id;
        probe.Method = HttpMethods.Delete;
        probe.Body = JsonSerializer.Serialize(new CatalogueDeleteInput(true, Guid.NewGuid().ToString(GuidFormat)), JsonSerializerOptions.Web);
        probe.Version = audience.Version;
        probe.Fault = GrantTransportFaults.Hold;
        using var response = await CrossBoundaryAsync(test, probe, Generation, proof);
        await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.Unauthorized);
        await GrantTransportTests.SafeAsync(response);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        Assert.Equal(1, probe.Requests);
        Assert.Equal(1, probe.Responses);
        Assert.Equal(0, probe.Redirects);
        await RecoverAsync(test, probe, audience);
    }

    /// <summary>Independently checks the entire retained approval and exact admission audit, then compares all persisted catalogue and audit fields after replay.</summary>
    private static async Task RecoverAsync(ApplicationHttpFixture test, GrantTransportProbe probe, AudienceDetails audience)
    {
        await using var observer = test.Environment.Api!.Services.CreateAsyncScope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var job = await database.Set<CatalogueDeletion>().AsNoTracking().SingleAsync(token);
        Assert.Equal(CatalogueDeletionState.Queued, job.State);
        Assert.Equal(audience.Id, job.TargetId);
        Assert.Null(job.CompletionAuditId);
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == Admitted).ToArrayAsync(token));
        Assert.Equal(job.AdmissionAuditId, audit.Id);
        Assert.Equal(job.ActorId, audit.ActorId);
        Assert.Equal(job.TargetId, audit.TargetId);
        var before = await CatalogueHttpEvidence.SnapshotAsync(test);
        probe.Path = string.Empty;
        using var recovered = await test.CatalogueAsync(HttpMethod.Delete, Api + Separator + audience.Id, probe.Body, audience.Version);
        Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
        Assert.Equal(job.Id, (await CatalogueHttpEvidence.JobAsync(recovered)).Id);
        Assert.Equal(before, await CatalogueHttpEvidence.SnapshotAsync(test));
    }

    /// <summary>Always releases and joins both the browser request and genuine upstream middleware before disposing their hosts.</summary>
    private static async Task<HttpResponseMessage> CrossBoundaryAsync(ApplicationHttpFixture test, GrantTransportProbe probe, string boundary, string? proof = null)
    {
        var token = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(WatchdogSeconds);
        var pending = test.CatalogueAsync(probe.Method == HttpMethods.Delete ? HttpMethod.Delete : HttpMethod.Get,
            Browser + probe.Path, probe.Body, probe.Version, direct: false, csrf: proof);
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(probe.Entered.Task, pending).WaitAsync(timeout, token);
            Assert.True(probe.Entered.Task.IsCompletedSuccessfully);
            Assert.False(pending.IsCompleted);
            if (boundary == Deadline)
            {
                await probe.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(MaximumSeconds), token);
            }
            else
            {
                await ChangeBrowserAsync(test, boundary);
            }
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

    /// <summary>Changes actual durable browser state; an extended current session cannot renew the held request's original authority.</summary>
    private static async Task ChangeBrowserAsync(ApplicationHttpFixture test, string boundary, bool shorten = false)
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>();
        var affected = shorten
            ? await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(1).UtcTicks), TestContext.Current.CancellationToken)
            : boundary == Generation
                ? await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), TestContext.Current.CancellationToken)
                : await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(ExtendedSeconds).UtcTicks), TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        if (boundary != Expiry || shorten)
        {
            return;
        }
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(1);
        using var current = await test.Browser.GetAsync(Session, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var status = JsonDocument.Parse(await current.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(status.RootElement.GetProperty(Authenticated).GetBoolean());
    }
}
