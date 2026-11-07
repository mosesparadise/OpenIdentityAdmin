using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Separately witnesses successful publication without a generation commit and without actual native scope cleanup.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotPublicationMutationTests(BootstrapFixture fixture)
{
    private const string CommitBypass = "pilot-publication-without-commit-bypass";
    private const string CleanupBypass = "pilot-publication-before-cleanup-bypass";

    /// <summary>Public cryptographic success must correspond to both exact native entries committed by this exchange.</summary>
    [Theory, Trait("CriticalDecision", "pilot-publication-commit")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GeneratedPilotTokens_When_SuccessIsPublished_Then_TheirExactNativeEntriesAreCommitted(string provider)
    {
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var code = await PilotMutationFlow.CodeAsync(environment, pilot.Client, flow);
        using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        var committed = await PilotMutationFlow.IsCommittedSuccessAsync(environment, pilot.Client, flow, response);
        Assert.False(await PilotMutationFlow.IsUnconsumedAsync(environment, code));
        Assert.True(committed, CommitBypass);
    }

    /// <summary>Only an observed actual native disposal failure may justify denial; committed success before that disposal is a demonstrated bypass.</summary>
    [Theory, Trait("CriticalDecision", "pilot-publication-cleanup")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedGenerationAndCleanupFault_When_ResponseCompletes_Then_NoCredentialPrecedesActualCleanup(string provider)
    {
        var fault = new PilotGenerationFault(PilotGenerationFailure.Disposal);
        var scopes = new PilotMutationScopes();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot,
            configureIssuer: services => { fault.Configure(services); scopes.Configure(services); }, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, TestContext.Current.CancellationToken);
        var evidence = await PilotGenerationEvidence.BeforeAsync(environment, identity.Item1, TestContext.Current.CancellationToken);
        fault.RequireConsumed = () => evidence.RequireConsumedAsync(environment, TestContext.Current.CancellationToken);
        fault.Armed = scopes.Armed = true;
        var bypass = false;
        try
        {
            using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1, fault.Inserts);
            Assert.Equal(1, fault.Saves);
            Assert.Equal(1, fault.Commits);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal(0, fault.Disposals);
                Assert.Equal(0, fault.Failures);
                Assert.True(await PilotMutationFlow.IsCommittedSuccessAsync(environment, pilot.Client, flow, response));
                bypass = true;
            }
            else
            {
                await RequireCleanupFailureAsync(response, fault);
            }
            fault.Armed = false;
            await PilotMutationFlow.RequireFreshAsync(environment, pilot.Client);
        }
        finally { fault.Armed = false; await scopes.DrainAsync(fault.Failure); }
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Failures);
        Assert.False(bypass, CleanupBypass);
    }

    /// <summary>Requires empty unavailable output and the exact reached disposal fault, never an arbitrary HTTP failure.</summary>
    private static async Task RequireCleanupFailureAsync(HttpResponseMessage response, PilotGenerationFault fault)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(PilotGenerationFault.StagedHeader));
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Failures);
    }
}
