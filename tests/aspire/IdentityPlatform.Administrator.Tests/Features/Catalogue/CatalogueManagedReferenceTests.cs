using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Checks durable deletion's explicit referenced outcome for retained managed interactions independently of application assignments.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueManagedReferenceTests(BootstrapFixture fixture)
{
    private const int DigestLength = 64;
    private const long Lifetime = 300;
    private const long Retention = 1_210_000;
    private const string Application = "synthetic-retained-client";

    /// <summary>A retained interaction added after admission prevents deletion with the correct terminal reason, without relying on an FK exception.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmittedCatalogueDeletion_When_ManagedReferenceAppears_Then_WorkerRetainsTargetAndReportsReferenced(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        await using (var observer = test.Scope())
        {
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var policy = await database.Set<ScopePolicy>().AsNoTracking().SingleAsync(test.Token);
            var now = test.Clock.Now.ToUnixTimeSeconds();
            var row = new ManagedLoginInteraction
            {
                SelectorDigest = new string('A', DigestLength),
                BrowserDigest = new string('B', DigestLength),
                DemandDigest = new string('C', DigestLength),
                ApplicationId = Application,
                ApplicationRevision = 1,
                Revision = 1,
                State = ManagedLoginState.Expired,
                CreatedAt = now,
                ExpiresAt = now + Lifetime,
                ObservedAt = now,
                RetainUntil = now + Retention,
                CorrelationId = Guid.NewGuid()
            };
            database.AddRange(row, new ManagedScopeReference { SelectorDigest = row.SelectorDigest, ScopeId = policy.ScopeId });
            await database.SaveChangesAsync(test.Token);
        }
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Id));
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.Referenced);
        await using var read = test.Scope();
        Assert.Single(await read.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ScopePolicy>().AsNoTracking().ToArrayAsync(test.Token));
    }
}
