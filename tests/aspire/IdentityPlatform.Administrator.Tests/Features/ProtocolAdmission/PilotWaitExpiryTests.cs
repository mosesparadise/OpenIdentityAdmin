using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises equality at the exact original persisted code deadline after native signature validation has already reached the gate.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotWaitExpiryTests(BootstrapFixture fixture)
{
    private const int LastSecond = 1;

    /// <summary>Advances only the shared test clock by one second while actually blocked; neither HTTP nor transaction lifetime is replaced.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_OriginalCodeAtLastSecond_When_ItExpiresDuringObservedGateWait_Then_NoRenewalConsumptionOrGenerationOccurs(string provider, bool standalone)
    {
        var probe = new PilotWaitProbe();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: services => Configure(services, probe, clock), clock: clock);
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var state = await PilotWaitState.ReadAsync(environment, pilot.Client, flow);
        var before = await PilotWaitState.SnapshotAsync(environment);
        clock.Now = state.Expiration.AddSeconds(-LastSecond);
        await using var gate = await PilotWaitGate.AcquireAsync(environment);
        probe.Armed = true;
        var pending = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await gate.ObserveAsync(environment, provider, probe, pending);
            clock.Now = state.Expiration;
            await gate.ReleaseAsync();
            using var response = await pending;
            await PilotWaitState.RejectedAsync(response);
            Assert.Equal(before, await PilotWaitState.SnapshotAsync(environment));
            await PilotRevokedAuthorization.RequireUnconsumedAsync(environment, state.CodeId);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await gate.CompleteAsync(pending, failure); }
        await RequireFreshNativeFlowAsync(environment, pilot.Client, state);
    }

    /// <summary>Proves fresh native issuance at the advanced fixture time without resetting the clock or claiming independent real-UTC JWT validation.</summary>
    private static async Task RequireFreshNativeFlowAsync(BrowserHttpsFixture environment, TestRelyingParty client, PilotWaitState state)
    {
        var fresh = await PilotReplayJourney.AuthorizeAsync(environment, client);
        var freshState = await PilotWaitState.ReadAsync(environment, client, fresh);
        Assert.False(string.Equals(state.CodeId, freshState.CodeId, StringComparison.Ordinal));
        Assert.True(freshState.Expiration > state.Expiration);
        using var accepted = await client.ExchangeAsync(fresh.Code, fresh.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await InitialPilotReplayEvidence.RequireCodeAsync(environment, freshState.CodeId, revoked: false);
    }

    /// <summary>Aligns native and product test clocks in both compositions, retaining unchanged native lifetimes and real watchdog timers.</summary>
    private static void Configure(IServiceCollection services, PilotWaitProbe probe, FactorClock clock)
    {
        probe.Configure(services);
        services.AddOptions<OpenIddictServerOptions>().Configure(options => options.TimeProvider = clock);
    }
}
