using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Retains actual password, MFA and consent forms across lifecycle transitions and proves none can resume after re-enable.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecycleHumanTests(BootstrapFixture fixture)
{
    private const string UnknownPhase = "Unknown human phase.";

    private const long DisabledRevision = 2;
    private const int Password = 0;
    private const int Mfa = 1;
    private const int Consent = 2;

    /// <summary>Both GET and protected POST fail without consuming proof or changing authority while disabled and after a new lifecycle revision is enabled.</summary>
    [Theory]
    [InlineData("PostgreSql", Password, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Mfa, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Consent, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Password, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Mfa, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Consent, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PendingHumanPhase_When_DisabledThenEnabled_Then_OriginalFormNeverRegainsAuthority(string provider, int phase)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var (form, location) = await PrepareAsync(test, phase);
        foreach (var enabled in new[] { false, true })
        {
            await ManagedLifecycleJourneyTests.TransitionAsync(test, enabled, enabled ? DisabledRevision : 1);
            var before = await ManagedJourneyEvidence.DigestAsync(test);
            using var page = await test.Http.IssuerClient.GetAsync(location, TestContext.Current.CancellationToken);
            await ManagedCacheTestContext.UnavailableAsync(page);
            using var posted = phase switch
            {
                Password => await ManagedHumanHttp.PasswordAsync(test, form),
                Mfa => await ManagedHumanHttp.MfaAsync(test, form),
                Consent => await ManagedHumanHttp.DecideAsync(test, form),
                _ => throw new InvalidOperationException(UnknownPhase)
            };
            await ManagedCacheTestContext.UnavailableAsync(posted);
            Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        }
        var fresh = new ManagedTokenFlow(test);
        await fresh.ApproveAsync();
        await ManagedJourneyEvidence.ExchangeAsync(test, fresh, await fresh.CodeAsync(), denyManagement: true);
    }

    /// <summary>Advances genuine native forms only to the selected phase, retaining that original protected form and issuer-local URL.</summary>
    private static async Task<(Dictionary<string, string> Form, Uri Location)> PrepareAsync(ManagedCacheTestContext test, int phase)
    {
        var flow = new ManagedTokenFlow(test);
        await flow.CacheAsync();
        using var entry = await test.SendAsync(flow.Resume);
        await ManagedHumanHttp.EntryAsync(entry);
        var location = entry.Headers.Location!;
        var form = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, location);
        if (phase >= Mfa)
        {
            using var password = await ManagedHumanHttp.PasswordAsync(test, form);
            await ManagedHumanHttp.EntryAsync(password);
            location = password.Headers.Location!;
            form = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, location);
        }
        if (phase == Consent)
        {
            using var factor = await ManagedHumanHttp.MfaAsync(test, form);
            await ManagedHumanHttp.EntryAsync(factor);
            location = factor.Headers.Location!;
            form = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, location, test.CallbackUri);
        }
        return (form, location);
    }
}
