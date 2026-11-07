using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves current independently committed authority after real provider waits through both genuine HTTPS pilot entry paths.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotWaitTests(BootstrapFixture fixture)
{
    internal const string Account = "account";
    internal const string Session = "session";
    internal const string Application = "application";
    internal const string Scope = "scope";
    internal const string Authorization = "authorization";

    /// <summary>A pre-wait valid code cannot consume or generate after one current authority changes; restored eligibility admits only a new genuine flow.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Account, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Session, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Application, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Scope, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Authorization, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Account, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Session, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Application, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Scope, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Authorization, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Account, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Session, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Application, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Scope, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Authorization, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Account, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Session, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Application, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Scope, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Authorization, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PilotExchangeWaitingOnGate_When_CurrentAuthorityIsIndependentlyRestricted_Then_NoConsumptionOrGenerationOccurs(string provider, bool standalone, string authority)
    {
        var probe = new PilotWaitProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var state = await PilotWaitState.ReadAsync(environment, pilot.Client, flow);
        await RestrictWhileWaitingAsync(environment, provider, pilot.Client, flow, state, probe, authority);
        await state.RestoreAsync(environment, authority);
        await PilotWaitState.PositiveAsync(environment, pilot.Client);
    }

    /// <summary>Retains the real holder until the separate authority mutation commits, then joins rejection and verifies complete native immutability.</summary>
    private static async Task RestrictWhileWaitingAsync(BrowserHttpsFixture environment, string provider, TestRelyingParty client,
        TestFlow flow, PilotWaitState state, PilotWaitProbe probe, string authority)
    {
        await using var gate = await PilotWaitGate.AcquireAsync(environment);
        probe.Armed = true;
        var pending = client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await gate.ObserveAsync(environment, provider, probe, pending);
            await state.RestrictAsync(environment, authority);
            var restricted = await PilotWaitState.SnapshotAsync(environment);
            Assert.False(pending.IsCompleted);
            await gate.ReleaseAsync();
            using var response = await pending;
            await PilotWaitState.RejectedAsync(response);
            Assert.Equal(restricted, await PilotWaitState.SnapshotAsync(environment));
            await PilotRevokedAuthorization.RequireUnconsumedAsync(environment, state.CodeId);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await gate.CompleteAsync(pending, failure); }
    }
}
