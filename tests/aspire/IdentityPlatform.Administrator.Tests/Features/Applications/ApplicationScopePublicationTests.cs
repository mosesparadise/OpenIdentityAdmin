using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Holds genuine assignment responses after API cleanup to prove original browser generation controls publication independently of remote commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopePublicationTests(BootstrapFixture fixture)
{
    private const string ScopeCollection = "/api/v1/scopes";
    private const int WatchdogSeconds = 30;
    private const int Ok = 200;
    private const string Assigned = "ApplicationScopesAssigned";
    private const string Email = "email";

    /// <summary>One held GET or committed PUT cannot publish under a replaced browser generation; explicit direct read recovers persisted state without another write.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CompletedAssignmentApiWork_When_BrowserGenerationChanges_Then_OutputIsSuppressedAndExplicitReadRecoversCommit(string provider, bool write)
    {
        var probe = new GrantTransportProbe { SuccessStatus = Ok };
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: services => services.AddSingleton<IStartupFilter>(probe));
        using var scope = await test.CatalogueAsync(HttpMethod.Post, ScopeCollection, ApplicationScopeHttpTests.Scope);
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var id = (await ApplicationHttpTests.CreatedAsync(create)).Application.Id;
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, id);
        await probe.BindAsync(test);
        probe.Path = ApplicationScopeHttpTests.Path(id, direct: true);
        probe.Method = write ? HttpMethods.Put : HttpMethods.Get;
        probe.Body = write ? ApplicationScopeHttpTests.Assign : null;
        probe.Version = write ? create.Headers.ETag!.ToString() : null;
        probe.Fault = GrantTransportFaults.Hold;
        using var response = await CrossAsync(test, probe, id, write);
        await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Null(response.Headers.ETag);
        Assert.Equal(1, probe.Requests);
        Assert.Equal(1, probe.Responses);
        Assert.Equal(0, probe.Redirects);
        var after = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, id);
        Assert.Equal(before.Policy.Revision + (write ? 1 : 0), after.Policy.Revision);
        Assert.Equal(write ? 1 : 0, after.Audits.Count(row => row.Event == Assigned));
        probe.Path = string.Empty;
        using var read = await test.DirectGetAsync(ApplicationScopeHttpTests.Path(id, direct: true));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var current = JsonSerializer.Deserialize<ApplicationScopes>(await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), JsonSerializerOptions.Web)!;
        Assert.Equal(write ? new[] { Email } : [], current.Scopes);
        Assert.Equal(after.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, id)).Digest);
    }

    /// <summary>Joins both native request and held response on every path and changes only actual durable browser generation.</summary>
    private static async Task<HttpResponseMessage> CrossAsync(ApplicationHttpFixture test, GrantTransportProbe probe, string id, bool write)
    {
        var token = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(WatchdogSeconds);
        var proof = write ? await test.TokenAsync() : null;
        var pending = test.CatalogueAsync(write ? HttpMethod.Put : HttpMethod.Get, ApplicationScopeHttpTests.Path(id), probe.Body, probe.Version, direct: false, csrf: proof);
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(probe.Entered.Task, pending).WaitAsync(timeout, token);
            Assert.True(probe.Entered.Task.IsCompletedSuccessfully);
            Assert.False(pending.IsCompleted);
            await using var scope = test.Environment.Client.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>()
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), token));
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
