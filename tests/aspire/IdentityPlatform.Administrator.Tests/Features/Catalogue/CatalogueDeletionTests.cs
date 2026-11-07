using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires durable idempotent admission and terminal native/product/audit deletion on both provider implementations.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionTests(BootstrapFixture fixture)
{
    private const string AdmissionEvent = "CatalogueDeletionAdmitted";
    private const string GuidFormat = "D";
    private const int AdvanceSeconds = 1;

    /// <summary>An independent worker root can finish accepted work, while replay before and after deletion never renews approval or duplicates evidence.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurableDeletion_When_IndependentWorkerCompletesAndRequestReplays_Then_OneOriginalApprovalAndTerminalAuditRemain(string provider, bool scope)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope);
        var correlation = Guid.NewGuid();
        var accepted = await CatalogueDeletionTest.AdmitAsync(test, intent, correlation: correlation);
        Assert.Equal(202, accepted.Status);
        await CatalogueMutationEvidence.RequireAuditAsync(test, correlation, AdmissionEvent, scope ? CatalogueAssertions.ScopeTarget : CatalogueAssertions.AudienceTarget, intent.Id, test.Clock.Now.ToUnixTimeSeconds());
        Assert.Equal("queued", accepted.Job!.State);
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Job.Id));
        Assert.True(original.ApprovalUntil > original.CreatedAt);
        Assert.True(original.ApprovalUntil <= accepted.Job.ExpiresAt.ToUnixTimeSeconds());
        test.Clock.Now = test.Clock.Now.AddSeconds(AdvanceSeconds);
        var replay = await CatalogueDeletionTest.AdmitAsync(test, intent);
        Assert.Equal(accepted.Job.Id, replay.Job!.Id);
        Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Job.Id)));
        var differentKey = CatalogueDeleteIntent.Create(intent.Resource, intent.Id, intent.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
        Assert.Equal(409, (await CatalogueDeletionTest.AdmitAsync(test, differentKey)).Status);
        await using var worker = test.Peer(provider);
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Job.Id, worker));
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Job.Id, CatalogueDeletionFailure.None);
        var completed = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Job.Id));
        Assert.Equal(original.ApprovalUntil, completed.ApprovalUntil);
        Assert.Equal("succeeded", (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!.State);
        Assert.Equal(completed, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Job.Id)));
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<OpenIddictEntityFrameworkCoreScope>().AnyAsync(row => row.Id == intent.Id, test.Token));
        Assert.Equal(3, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetId == intent.Id, test.Token));
    }
}
