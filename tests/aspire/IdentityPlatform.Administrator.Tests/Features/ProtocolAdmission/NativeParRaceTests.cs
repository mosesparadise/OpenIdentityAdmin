using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves a real PAR reference cannot produce two authorization codes across independent concurrently active issuer roots.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeParRaceTests(BootstrapFixture fixture)
{
    /// <summary>The second actual authorize request must contend on the first's exact pre-update redemption transaction and then reject the consumed PAR.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OneGenuineParAcrossTwoIssuers_When_AuthorizeRequestsContendAtRedemption_Then_OnlyOneCodeIsIssued(string provider)
    {
        var winnerProbe = new NativeParRaceProbe();
        var peerProbe = new NativeParRaceProbe();
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: winnerProbe.Configure);
        await using var peer = scenario.CreatePeer(provider, peerProbe.Configure);
        await WarmAsync(scenario, peer.Services);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        var gate = winnerProbe.Redemption.Arm(identifier);
        var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new NativeLockObservation(provider, scenario.Account.Journey.Lease.AdministratorConnection);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var outcomes = await NativeBlockedRace.InvokeAsync(() => NativeAccountFlow.AuthorizeAsync(scenario, reference), gate.Reached.Task, () =>
        {
            peerProbe.BeforeLock = session;
            return NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services);
        }, session.Task, gate.Release, observer.WaitAsync);
        var code = NativeAccountFlow.ReadCode(scenario, outcomes[0]);
        NativeIssuerReplayTests.RequireParReplay(outcomes[1]);
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, identifier));
        Assert.Equal((before.Tokens + 1, before.Authorizations + 1), await NativeAccountPersistence.CountAsync(scenario));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var issued = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), principal, issued);
    }

    /// <summary>Completes a genuine peer grant before holding a production transaction, avoiding cold service preparation inside the lock budget.</summary>
    private static async Task WarmAsync(AdministratorProtocolScenario scenario, IServiceProvider peer)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer), principal, before);
    }
}
