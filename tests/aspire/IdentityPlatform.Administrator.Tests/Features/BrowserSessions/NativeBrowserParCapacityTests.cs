using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Verifies capacity-denied native cleanup cannot persist an observed but unattached completion.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserParCapacityTests(BootstrapFixture fixture)
{
    private const int LiveRequestLimit = 128;
    private const int ObservationAdvanceSeconds = 7;

    /// <summary>Full live capacity returns the native quota error with the exact completion untouched, even after its later observation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompletedLoginAndFullLiveParBudget_When_NativeReservationDenies_Then_CompletionAndAttachmentAuditRemainUnchanged(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        await NativeTokenBudgetFixture.FillAsync(flow.Services, LiveRequestLimit, false,
            OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, OpenIddictConstants.Statuses.Valid,
            TestContext.Current.CancellationToken);
        flow.Journey.Clock.Now = flow.Journey.Clock.Now.AddSeconds(ObservationAdvanceSeconds);
        var completion = await flow.RowAsync();
        Assert.True(flow.Journey.Clock.Now.ToUnixTimeSeconds() > completion.ObservedAt);
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        NativeAccountDenialTests.RequireJson(await flow.PushAsync(), OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        Assert.Equal(IssuerContinuationProviderFixture.Completed, (await flow.RowAsync()).State);
    }
}
