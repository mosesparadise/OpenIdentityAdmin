using System.Globalization;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Separates lifecycle enablement from actual managed credential expiry through real BFF and issuer HTTPS.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecycleCredentialTests(BootstrapFixture fixture)
{
    private const string Display = "Short-lived managed credential";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const long DisabledRevision = 2;
    private const int CredentialSeconds = 60;

    /// <summary>Enabling an expired registration preserves the hash and credential history and cannot admit a new issuer request.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CredentialExpiresWhileDisabled_When_Reenabled_Then_StateChangesButOriginalExpiryStillPreventsAdmission(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var expiry = http.Now.AddSeconds(CredentialSeconds);
        var input = new ApplicationCreate(ManagedCacheTestContext.Client, Display, [ManagedCacheTestContext.Callback],
            expiry.ToString(ExpiryFormat, CultureInfo.InvariantCulture));
        using var response = await http.CreateAsync(JsonSerializer.Serialize(input, JsonSerializerOptions.Web));
        var test = new ManagedCacheTestContext(http, await ApplicationHttpTests.CreatedAsync(response));
        using var admitted = await test.SendAsync(ManagedCacheTestContext.Initial());
        Assert.Equal(HttpStatusCode.Redirect, admitted.StatusCode);
        await test.ResumeAsync(admitted);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, false, 1);
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(http, test.Id);
        http.Environment.Issuer.Account.Journey.Clock.Now = expiry;
        await ManagedLifecycleJourneyTests.TransitionAsync(test, true, DisabledRevision);
        var enabled = await ApplicationHttpUpdateEvidence.SnapshotAsync(http, test.Id);
        Assert.True(enabled.Policy.Enabled);
        Assert.Equal(before.NativeDigest, enabled.NativeDigest);
        Assert.Equal(before.Policy.CredentialRevision, enabled.Policy.CredentialRevision);
        Assert.Equal(before.Policy.CredentialIssuedAt, enabled.Policy.CredentialIssuedAt);
        Assert.Equal(expiry.ToUnixTimeSeconds(), enabled.Policy.CredentialExpiresAt);
        Assert.Equal(before.Policy.CredentialExpiresAt, enabled.Policy.CredentialExpiresAt);
        Assert.Equal(before.Policy.Revision + 1, enabled.Policy.Revision);
        var unchanged = await ManagedJourneyEvidence.DigestAsync(test);
        using var denied = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(denied);
        Assert.Equal(unchanged, await ManagedJourneyEvidence.DigestAsync(test));
    }
}
