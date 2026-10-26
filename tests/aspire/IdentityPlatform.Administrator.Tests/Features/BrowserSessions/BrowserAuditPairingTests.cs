using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Prevents canonical audit metadata from legitimizing a completion belonging to another genuine login.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserAuditPairingTests(BootstrapFixture fixture)
{
    private const string Link = "administrator-browser-audit-link";

    /// <summary>Two real password/MFA completions cannot be cross-paired; the original untouched pair remains usable exactly once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TwoGenuineCompletions_When_OneIntentUsesTheOtherProof_Then_NoAuditLinkCommits(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        var other = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(flow.Journey, other));
        var password = await IssuerContinuationProviderFixture.BeginAsync(flow.Journey, other);
        Assert.NotNull(password.Interaction);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(flow.Journey);
        var grant = await IssuerContinuationProviderFixture.CompleteAsync(flow.Journey, other, password.Interaction, code);
        Assert.NotNull(grant?.BrowserCompletion);
        var first = await flow.RowAsync();
        var second = await IssuerContinuationProviderFixture.RowAsync(flow.Journey, other);
        Assert.True(first.CompletionDigest != second.CompletionDigest && first.SessionId != second.SessionId);
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync(completionOverride: grant.BrowserCompletion));
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        var untouched = await IssuerContinuationProviderFixture.RowAsync(flow.Journey, other);
        Assert.Equal(second.Revision, untouched.Revision);
        Assert.Equal(second.State, untouched.State);
        Assert.Null(untouched.RequestId);
        await using var scope = flow.Journey.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
            .Where(row => row.Event == Link).ToArrayAsync(TestContext.Current.CancellationToken));
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), first, flow.Journey.Clock.Now.ToUnixTimeSeconds());
    }
}
