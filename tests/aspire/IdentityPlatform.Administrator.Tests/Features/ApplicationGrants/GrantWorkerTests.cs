using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Worker.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Executes the actual hosted Worker composition against a migrated isolated provider database, including process-lifetime recovery.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantWorkerTests(BootstrapFixture fixture)
{
    private const string ProviderSetting = "Database:Provider";
    private const string ConnectionSetting = "ConnectionStrings:platform";
    private const int MultipleBatches = 33;
    private const int WorkerCount = 2;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Requires real background polling to commit progress, drain shutdown and complete remaining work from an independent restarted host.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_WorkerRestartsAfterPartialProgress_Then_DurableRemainingWorkCompletes(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, MultipleBatches, MultipleBatches);
        var accepted = await test.AdmitAsync(test.Intent());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using (var first = Host(test, provider))
        {
            await first.StartAsync(deadline.Token);
            await ObserveAsync(test, accepted.Id, complete: false, deadline.Token);
            await first.StopAsync(deadline.Token);
        }
        var partial = await test.ReadAsync(accepted.Id);
        Assert.Equal(GrantJobState.Processing, partial.State);
        Assert.InRange(partial.TokenCount, 1, MultipleBatches - 1);
        await using (var restarted = Host(test, provider))
        {
            await restarted.StartAsync(deadline.Token);
            await ObserveAsync(test, accepted.Id, complete: true, deadline.Token);
            await restarted.StopAsync(deadline.Token);
        }
        var completed = await test.ReadAsync(accepted.Id);
        Assert.Equal(MultipleBatches, completed.TokenCount);
        Assert.Equal(MultipleBatches, completed.AuthorizationCount);
        Assert.Equal(partial.EffectiveApprovalId, completed.EffectiveApprovalId);
        Assert.All((await test.StatusesAsync(test.Id)).Tokens, value => Assert.Equal("revoked", value));
    }

    /// <summary>Requires two actual background hosts to wait in the provider lock graph before release, then proves nonoverlapping audited batches and one completion.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoHostedWorkers_When_BothActuallyWaitOnGovernance_Then_OnlyDisjointNativeTransitionsAreCounted(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, MultipleBatches, MultipleBatches);
        var accepted = await test.AdmitAsync(test.Intent());
        var lease = test.Fixture.Scenario.Account.Journey.Lease;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using var first = Host(test, provider);
        await using var second = Host(test, provider);
        await using var held = await GovernanceLock.AcquireAsync(lease);
        try
        {
            await first.StartAsync(deadline.Token);
            await second.StartAsync(deadline.Token);
            await held.ObserveOwnedAsync(lease, WorkerCount, deadline.Token);
        }
        finally { await held.DisposeAsync(); }
        await ObserveAsync(test, accepted.Id, complete: true, deadline.Token);
        await first.StopAsync(deadline.Token);
        await second.StopAsync(deadline.Token);
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        Assert.Equal(MultipleBatches, history.Job.AuthorizationCount);
        Assert.Equal(MultipleBatches, history.Job.TokenCount);
        Assert.Equal(7, history.Job.Revision);
        Assert.Single(history.Approvals);
        Assert.Single(history.Details, row => row.State == GrantJobState.Succeeded);
    }

    /// <summary>Uses production storage and grant registrations without borrowing the API's richer store-selection service graph.</summary>
    private static WebApplication Host(GrantTestContext test, string provider) =>
        CreateHost(provider, test.Fixture.Scenario.Account.Journey.Lease.RuntimeConnection, test.Fixture.Clock);

    /// <summary>Shares the actual production owned-store and Worker composition with retained-browser grant journeys.</summary>
    internal static WebApplication CreateHost(string provider, string connection, TimeProvider clock)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration[ProviderSetting] = provider;
        builder.Configuration[ConnectionSetting] = connection;
        builder.Services.AddSingleton(clock);
        builder.AddFeatures();
        return builder.Build();
    }

    /// <summary>Observes independently persisted progress with one test-owned deadline; this does not retry a mutation or invoke the batch coordinator.</summary>
    private static async Task<ApplicationGrantJob> ObserveAsync(GrantTestContext test, string id, bool complete, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var job = await test.ReadAsync(id);
            Assert.NotEqual(GrantJobState.Stopped, job.State);
            if (complete ? job.State == GrantJobState.Succeeded : job.TokenCount > 0)
            {
                return job;
            }
            await Task.Delay(ObservationInterval, token);
        }
    }
}
