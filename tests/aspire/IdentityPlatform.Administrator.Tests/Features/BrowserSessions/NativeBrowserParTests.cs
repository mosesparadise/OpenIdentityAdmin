using System.Text;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves native attachment, independent assertion replay, completion deadlines and dormant authorization on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserParTests(BootstrapFixture fixture)
{
    private const int AttachmentAdvanceSeconds = 7;
    private const int CompletionAdvanceSeconds = 40;
    private const int RejectedRollbackSeconds = 31;
    private const string LocalContentType = "text/plain;charset=UTF-8";
    private const string LocalError = "error:invalid_request";
    private const string LocalDescription = "error_description:The request could not be completed.";
    private const int LocalErrorFields = 2;

    /// <summary>Actual PAR attaches its real token ID and newer clock watermark once; a fresh authenticated replay cannot change that receipt.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompletedFreshLogin_When_NativeParAndNewAssertionReplayRun_Then_ExactlyOneNativeRequestAttaches(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        var before = await flow.RowAsync();
        flow.Journey.Clock.Now = flow.Journey.Clock.Now.AddSeconds(AttachmentAdvanceSeconds);
        var observation = flow.Journey.Clock.Now.ToUnixTimeSeconds();
        Assert.True(observation > before.ObservedAt);
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), before, observation);
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
        var committed = await flow.AttachmentSnapshotAsync();
        var assertions = await flow.AssertionsAsync();
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.True(committed == await flow.AttachmentSnapshotAsync());
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>Equality at original completion expiry denies after actual assertion admission, without attaching a token or rewriting completion.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompletedLoginAtExpiry_When_NativeParRuns_Then_NoAttachmentOrTokenCommits(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        flow.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds((await flow.RowAsync()).ExpiresAt);
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>A genuinely later completion rejects rollback beyond tolerance even while the older protocol watermark still permits fresh assertion admission.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompletionWatermarkAheadOfProtocol_When_ClockRollsBackBeyondTolerance_Then_AttachmentFailsClosed(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, CompletionAdvanceSeconds);
        var completion = await flow.RowAsync();
        flow.Journey.Clock.Now = flow.Journey.Clock.Now.AddSeconds(-RejectedRollbackSeconds);
        Assert.Equal(RejectedRollbackSeconds, completion.ObservedAt - flow.Journey.Clock.Now.ToUnixTimeSeconds());
        Assert.True(await NativeAccountPersistence.ObservedAtAsync(flow.Scenario) <= flow.Journey.Clock.Now.ToUnixTimeSeconds());
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>Ordinary B3 native authorization remains usable; a real bound request is rejected locally before any code or new session authority.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AttachedBrowserCompletion_When_AuthorizationRestoresNativeRequest_Then_DormantGuardPreventsIssuance(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        var ordinary = await NativeAccountFlow.PushAsync(flow.Scenario, flow.Services);
        _ = NativeAccountFlow.ReadCode(flow.Scenario, await NativeAccountFlow.AuthorizeAsync(flow.Scenario, ordinary, flow.Services));
        var before = await flow.RowAsync();
        var reference = await flow.RequireAttachedAsync(await flow.PushAsync(), before, flow.Journey.Clock.Now.ToUnixTimeSeconds());
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(flow.Scenario, reference);
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var security = await IssuerContinuationProviderFixture.SnapshotAsync(flow.Journey);
        var response = await NativeAccountFlow.AuthorizeAsync(flow.Scenario, reference, flow.Services);
        RequireLocalDenial(response);
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(flow.Scenario, identifier));
        Assert.True(security == await IssuerContinuationProviderFixture.SnapshotAsync(flow.Journey));
    }

    /// <summary>Checks the installed local formatter's finite body; no redirect, state, completion or cookie may survive.</summary>
    private static void RequireLocalDenial(NativeAccountResult response)
    {
        Assert.Null(response.Failure);
        Assert.False(response.Claimed);
        Assert.False(response.Aborted);
        Assert.False(response.HasCookie);
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        Assert.Empty(response.Location);
        Assert.Equal(LocalContentType, response.ContentType);
        var lines = Encoding.UTF8.GetString(response.Body).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length == LocalErrorFields && lines.Contains(LocalError, StringComparer.Ordinal) && lines.Contains(LocalDescription, StringComparer.Ordinal));
    }
}
