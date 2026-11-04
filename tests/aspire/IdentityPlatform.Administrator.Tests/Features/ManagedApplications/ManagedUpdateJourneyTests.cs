using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves real issuer recovery after protected HTTP configuration and credential changes, using original native requests and fresh human ceremonies.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedUpdateJourneyTests(BootstrapFixture fixture)
{
    private const string InvalidGrant = "invalid_grant";
    private const string InvalidClient = "invalid_client";
    private const string InvalidRequest = "invalid_request";
    private const string InvalidToken = "invalid_token";
    private const string UpdatedCallback = "https://updated.example.test/callback";

    /// <summary>Editing the callback invalidates original cached requests and codes while a fresh password/MFA/consent flow works at the new exact callback.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ManagedClientWithCachedRequestAndCode_When_CallbackIsEditedOverHttps_Then_OldConfigurationFailsAndNewIdentityOnlyJourneySucceeds(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var old = new ManagedTokenFlow(test);
        await old.ApproveAsync();
        var code = await old.CodeAsync();
        var cached = new ManagedTokenFlow(test);
        await cached.CacheAsync();
        using var edit = await test.Http.UpdateAsync(test.Id, ApplicationHttpUpdateEvidence.Edit, ApplicationVersion.Create(test.Id, 1));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var details = (await edit.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken))!;
        Assert.Equal(UpdatedCallback, Assert.Single(details.RedirectUris));
        var current = new ManagedCacheTestContext(test.Http, new(details, test.Secret));
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var rejectedCallback = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedTokenEvidence.ErrorAsync(rejectedCallback, HttpStatusCode.BadRequest, InvalidRequest, local: true);
        using var rejectedCache = await test.SendAsync(cached.Resume);
        await ManagedTokenEvidence.ErrorAsync(rejectedCache, HttpStatusCode.BadRequest, InvalidToken, local: true);
        using var rejectedCode = await old.ExchangeAsync(code);
        await ManagedTokenEvidence.ErrorAsync(rejectedCode, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        await CompleteCurrentAsync(current);
    }

    /// <summary>After replacement, old codes fail even with the new valid secret; a fresh code rejects the old secret and remains usable with the new one.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ManagedClientWithIssuedCode_When_SecretIsReplacedOverHttps_Then_OldCodeAndSecretFailIndependentlyAndNewCredentialsSucceed(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var old = new ManagedTokenFlow(test);
        await old.ApproveAsync();
        var code = await old.CodeAsync();
        using var response = await test.Http.UpdateAsync(test.Id, ApplicationHttpUpdateEvidence.Replacement(test.Http.Now), ApplicationVersion.Create(test.Id, 1), replacement: true);
        var output = await ApplicationHttpTests.CreatedAsync(response, HttpStatusCode.OK);
        Assert.True(output.ClientSecret != test.Secret);
        var current = new ManagedCacheTestContext(test.Http, output);
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var rejectedCode = await old.ExchangeAsync(code, secret: current.Secret);
        await ManagedTokenEvidence.ErrorAsync(rejectedCode, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        var authority = await ManagedHumanEvidence.AuthorityAsync(current);
        var fresh = new ManagedTokenFlow(current);
        await fresh.ApproveAsync();
        var freshCode = await fresh.CodeAsync();
        before = await ManagedJourneyEvidence.DigestAsync(current);
        using var rejectedSecret = await fresh.ExchangeAsync(freshCode, secret: test.Secret);
        await ManagedTokenEvidence.ErrorAsync(rejectedSecret, HttpStatusCode.Unauthorized, InvalidClient);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(current));
        await ManagedJourneyEvidence.ExchangeAsync(current, fresh, freshCode, denyManagement: true);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(current));
    }

    /// <summary>Uses current metadata for a new full native journey, verifying actual cryptography, audit correlation and denied administrator API access.</summary>
    private static async Task CompleteCurrentAsync(ManagedCacheTestContext current)
    {
        var authority = await ManagedHumanEvidence.AuthorityAsync(current);
        var fresh = new ManagedTokenFlow(current);
        await fresh.ApproveAsync();
        await ManagedJourneyEvidence.ExchangeAsync(current, fresh, await fresh.CodeAsync(), denyManagement: true);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(current));
    }
}
