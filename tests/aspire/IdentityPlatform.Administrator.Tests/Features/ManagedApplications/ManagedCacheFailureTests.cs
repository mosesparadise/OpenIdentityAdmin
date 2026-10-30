using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves actual cache publication rejects late native/policy/interaction mutation and original deadline expiry on both databases.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheFailureTests(BootstrapFixture fixture)
{
    private const int LifetimeSeconds = 300;
    private const int ObservationAdvanceSeconds = 2;
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Each causal fault fires after request attachment saved, yet no native request, interaction or response material escapes rollback.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_RealNativeCache_When_FinalAttachmentChangesOrFails_Then_EntireTransactionAndRedirectAreWithheld(string provider, ManagedAttachmentDefect defect)
    {
        var fault = new ManagedCacheAttachmentFault(defect);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var before = await test.SnapshotAsync();
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(LifetimeSeconds);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(ObservationAdvanceSeconds);
        fault.Armed = true;
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.True(fault.Fired);
        Assert.True(defect == ManagedAttachmentDefect.Throw || fault.MutationCompleted);
        var after = await test.SnapshotAsync();
        Assert.Empty(after.Tokens);
        Assert.Empty(after.Interactions);
        Assert.Equal(0, after.Authorizations);
        Assert.Equal(before.Sessions, after.Sessions);
    }

    /// <summary>Post-commit failure withholds the entire redirect/cookie while retaining one bounded attached interaction and no retry.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedNativeCache_When_AcknowledgementOrDisposalFails_Then_NoRedirectEscapesAndOneAttachedRowRemains(string provider, bool cleanup)
    {
        var fault = new ManagedCacheCompletionFault(cleanup);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Faults);
        var actual = await test.SnapshotAsync();
        Assert.Equal(Assert.Single(actual.Tokens).Id, Assert.Single(actual.Interactions).RequestId);
        Assert.Equal(0, actual.Authorizations);
    }

    /// <summary>Preserves explicit provider metadata and separately identified fault cases in native discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, ManagedAttachmentDefect>> Cases => from provider in Providers
                                                                                       from defect in Enum.GetValues<ManagedAttachmentDefect>()
                                                                                       select new TheoryDataRow<string, ManagedAttachmentDefect>(provider, defect).WithTrait(ProviderTrait, provider);
}
