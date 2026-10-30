using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises proof freshness and current authority through actual managed forms with immutable native request deadlines.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanDriftTests(BootstrapFixture fixture)
{
    private const int LastLiveSecond = 299;

    /// <summary>Every independent account, factor, policy and credential change rejects both MFA and consent without spending proof; restored authority permits the same form.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PasswordAndMfaBoundInteraction_When_CurrentAuthorityChanges_Then_EachPhaseFailsClosedWithoutProofConsumption(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await ManagedHumanHttp.StartAsync(test);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(1);
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(test.Http.Environment.Issuer.Account.Journey);
        foreach (var defect in Enum.GetValues<ManagedHumanAuthority>())
        {
            await ManagedHumanDrift.CheckAsync(test, defect, () => ManagedHumanHttp.MfaAsync(test, mfa, code));
        }
        using var accepted = await ManagedHumanHttp.MfaAsync(test, mfa, code);
        await ManagedHumanHttp.EntryAsync(accepted);
        var consent = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, accepted.Headers.Location!);
        foreach (var defect in Enum.GetValues<ManagedHumanAuthority>())
        {
            await ManagedHumanDrift.CheckAsync(test, defect, () => ManagedHumanHttp.DecideAsync(test, consent));
        }
        using var approved = await ManagedHumanHttp.DecideAsync(test, consent);
        await ManagedHumanHttp.EntryAsync(approved);
        Assert.Equal(ManagedLoginState.Approved, Assert.Single((await test.SnapshotAsync()).Interactions).State);
    }

    /// <summary>A password accepted in the last live second retains the original absolute expiry; equality cannot perform MFA or renew the flow.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OriginalHumanDeadline_When_PasswordIsLateAndMfaArrivesAtEquality_Then_DeadlineNeverRenews(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await ManagedHumanHttp.StartAsync(test);
        var original = Assert.Single((await test.SnapshotAsync()).Interactions);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.CreatedAt + LastLiveSecond);
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        var before = await ManagedHumanEvidence.DigestAsync(test);
        Assert.Equal(original.ExpiresAt, Assert.Single((await test.SnapshotAsync()).Interactions).ExpiresAt);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.ExpiresAt);
        using var expired = await ManagedHumanHttp.MfaAsync(test, mfa);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }
}
