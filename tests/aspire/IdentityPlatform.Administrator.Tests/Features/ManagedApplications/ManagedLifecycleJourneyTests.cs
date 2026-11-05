using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises current lifecycle writes through real BFF/issuer HTTPS and retains original native artifacts across both transitions.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecycleJourneyTests(BootstrapFixture fixture)
{
    private const string Bypass = "application-lifecycle-enforcement-bypass";
    private const long DisabledRevision = 2;
    private const int NativeFactorStepSeconds = 30;
    private const string LifecycleInput = "{\"reason\":\"maintenance\",\"confirm\":true}";
    private const string OtherClient = "independent-managed-client";
    private const string InvalidToken = "invalid_token";
    private const string InvalidGrant = "invalid_grant";

    /// <summary>Disabling affects only the target; re-enable permits fresh proof but never revives its old cached request or authorization code.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests)), Trait("CriticalDecision", "application-lifecycle-enforcement")]
    public async Task Given_PendingCacheAndIssuedCode_When_DisabledThenEnabled_Then_OldArtifactsStayInvalidAndIndependentFreshJourneysWork(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var old = new ManagedTokenFlow(test);
        await old.ApproveAsync();
        var code = await old.CodeAsync();
        var cached = new ManagedTokenFlow(test);
        await cached.CacheAsync();
        using var otherResponse = await test.Http.CreateAsync(ApplicationHttpTests.Input(test.Http.Now, OtherClient));
        var other = new ManagedCacheTestContext(test.Http, await ApplicationHttpTests.CreatedAsync(otherResponse));
        await CompleteAsync(other);
        var unchanged = await ManagedJourneyEvidence.RegistrationAsync(other);
        await TransitionAsync(test, false, 1);
        Assert.Equal(unchanged, await ManagedJourneyEvidence.RegistrationAsync(other));
        using var admission = await test.SendAsync(ManagedCacheTestContext.Initial());
        Assert.False(admission.StatusCode == HttpStatusCode.Redirect, Bypass);
        await ManagedCacheTestContext.UnavailableAsync(admission);
        await RejectOldAsync(test, cached, old, code);
        await CompleteAsync(other);
        unchanged = await ManagedJourneyEvidence.RegistrationAsync(other);
        await TransitionAsync(test, true, DisabledRevision);
        Assert.Equal(unchanged, await ManagedJourneyEvidence.RegistrationAsync(other));
        await RejectOldAsync(test, cached, old, code);
        // Enrollment, administrator sign-in and three managed ceremonies exhausted the five native counters.
        // Wait for one real counter rollover before starting new proof; do not reuse a code or widen the native window.
        await Task.Delay(TimeSpan.FromSeconds(NativeFactorStepSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % NativeFactorStepSeconds + 1), TestContext.Current.CancellationToken);
        await CompleteAsync(test);
    }

    /// <summary>Uses genuine native antiforgery and exact current version, then checks the returned lifecycle and original credential validity.</summary>
    internal static async Task TransitionAsync(ManagedCacheTestContext test, bool enabled, long revision)
    {
        using var response = await test.Http.UpdateAsync(test.Id, LifecycleInput, ApplicationVersion.Create(test.Id, revision), enabled: enabled);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(enabled, details!.Enabled);
        Assert.Equal(ApplicationVersion.Create(test.Id, revision + 1), response.Headers.ETag!.Tag);
        Assert.Equal(test.ClientIdentifier, details.ClientId);
        Assert.Equal(test.CallbackUri, Assert.Single(details.RedirectUris));
    }

    /// <summary>Reuses the exact pre-transition artifacts and independently proves rejection has no hidden managed/native write.</summary>
    private static async Task RejectOldAsync(ManagedCacheTestContext test, ManagedTokenFlow cached, ManagedTokenFlow old, string code)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var request = await test.SendAsync(cached.Resume);
        await ManagedTokenEvidence.ErrorAsync(request, HttpStatusCode.BadRequest, InvalidToken, local: true);
        using var exchange = await old.ExchangeAsync(code);
        await ManagedTokenEvidence.ErrorAsync(exchange, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
    }

    /// <summary>Requires new password/MFA/consent and verifies native token cryptography without creating administrator authority.</summary>
    private static async Task CompleteAsync(ManagedCacheTestContext test)
    {
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        await ManagedJourneyEvidence.ExchangeAsync(test, flow, await flow.CodeAsync(), denyManagement: true);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }
}
