using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Distinguishes actual durable success with lost output from precommit rollback, without retrying a completed human phase.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanPublicationTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>The phase really commits once, but acknowledgement/disposal/callback faults withhold every redirect/cookie/body and reject old-form replay.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_RealPasswordCommit_When_PublicationBoundaryFails_Then_NoOutputEscapesAndOldFormCannotRepeatIt(string provider, ManagedHumanPublicationDefect defect)
    {
        var fault = new ManagedHumanPublicationFault(defect);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var form = await ManagedHumanHttp.StartAsync(test);
        var before = Assert.Single((await test.SnapshotAsync()).Interactions);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var now = test.Http.Now;
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(before.ExpiresAt);
        fault.Armed = true;
        using var denied = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedCacheTestContext.UnavailableAsync(denied);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Faults);
        var after = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(ManagedLoginState.PendingMfa, after.State);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
        fault.Armed = false;
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = now;
        using var replay = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedCacheTestContext.UnavailableAsync(replay);
        Assert.Equal(after.Revision, Assert.Single((await test.SnapshotAsync()).Interactions).Revision);
    }

    /// <summary>Preserves separately discovered provider and causal-boundary identities.</summary>
    public static IEnumerable<TheoryDataRow<string, ManagedHumanPublicationDefect>> Cases => from provider in Providers
                                                                                             from defect in Enum.GetValues<ManagedHumanPublicationDefect>()
                                                                                             select new TheoryDataRow<string, ManagedHumanPublicationDefect>(provider, defect).WithTrait(ProviderTrait, provider);
}
