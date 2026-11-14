using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Prevents a current scope identifier/revision from masking changed consent text at cache, approved-consent or code boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedConsentDriftTests(BootstrapFixture fixture)
{
    private const string Requested = "email openid";
    private const string IdentityKind = "identity";
    private const string ChangedConsent = "Different disclosure";

    /// <summary>Same-revision consent text replacement invalidates the exact earlier policy before human proof or native issuance can advance.</summary>
    [Theory]
    [InlineData("PostgreSql", ConsentBoundary.Cached, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ConsentBoundary.Displayed, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ConsentBoundary.Approved, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ConsentBoundary.Code, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", ConsentBoundary.Cached, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ConsentBoundary.Displayed, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ConsentBoundary.Approved, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ConsentBoundary.Code, Traits = ["Provider", "SqlServer"])]
    public async Task Given_BoundConsentPolicy_When_TextChangesWithoutRevisionAdvance_Then_EarlierCachedConsentOrCodeCannotIssue(string provider, ConsentBoundary boundary)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        var flow = new ManagedTokenFlow(test, Requested);
        await flow.CacheAsync();
        var form = boundary == ConsentBoundary.Displayed ? await flow.ConsentAsync() : null;
        if (boundary is ConsentBoundary.Approved or ConsentBoundary.Code)
        {
            await flow.DecideAsync();
        }
        var code = boundary == ConsentBoundary.Code ? await flow.CodeAsync() : null;
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = await database.Set<ScopePolicy>().SingleAsync(row => row.Kind == IdentityKind, TestContext.Current.CancellationToken);
        policy.ConsentText = ChangedConsent;
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var denied = form is not null ? await ManagedHumanHttp.DecideAsync(test, form) :
            code is null ? await test.SendAsync(flow.Resume) : await flow.ExchangeAsync(code);
        await ManagedCacheTestContext.UnavailableAsync(denied);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
    }
}

/// <summary>Names the retained native/human decisions whose complete original metadata remains binding.</summary>
public enum ConsentBoundary
{
    Cached, Displayed, Approved, Code
}
