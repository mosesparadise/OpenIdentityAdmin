using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Distinguishes a sealed issuance response committed before disable from newly issued credentials after disable.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecyclePublicationTests(BootstrapFixture fixture)
{
    private const int CaptureSeconds = 5;
    private const int ExchangeTokens = 2;

    /// <summary>Holds the real successful commit callback after native output sealing, disables through protected HTTPS, then releases the prior response.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_SealedCommittedIssuance_When_ResponseArrivesAfterDisable_Then_ItRemainsPriorIssuanceWithoutNewAuthority(string provider, bool exchange)
    {
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new ManagedCacheCompletionFault(cleanup: false, issuance: true) { Armed = false };
        fault.AfterCommit = async token => { committed.TrySetResult(); await release.Task.WaitAsync(token); };
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        fault.Armed = true;
        var pending = exchange ? flow.ExchangeAsync(code!) : test.SendAsync(flow.Resume);
        try
        {
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(CaptureSeconds), TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
            var sealedRows = await test.SnapshotAsync();
            Assert.Equal(before.Tokens.Length + (exchange ? ExchangeTokens : 1), sealedRows.Tokens.Length);
            await ManagedLifecycleJourneyTests.TransitionAsync(test, enabled: false, revision: 1);
            Assert.False((await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id)).Policy.Enabled);
            Assert.False(pending.IsCompleted);
            release.TrySetResult();
            using var response = await pending;
            Assert.Equal(1, fault.Commits);
            Assert.Equal(1, fault.Disposals);
            Assert.Equal(0, fault.Faults);
            fault.Armed = false;
            await ManagedLifecycleRaceTests.RequireOutputAsync(test, flow, response, exchange, issuanceFirst: true);
        }
        finally
        {
            release.TrySetResult();
            try
            {
                using var drained = await pending;
            }
            catch { /* The primary assertion or operation failure remains authoritative after joining the request. */ }
        }
    }
}
