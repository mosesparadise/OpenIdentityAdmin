using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Exercises keyset progress in the real hosted poller past a full page of persistently invalid admission evidence.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionQueueTests(BootstrapFixture fixture)
{
    private const int PageSize = 16;
    private const int Candidates = PageSize + 1;
    private const string Resource = "queue-api-";
    private const string Display = "Queue example";
    private const string Owner = "Operations";
    private const string InvalidOutcome = "Rejected";
    private const string GuidFormat = "D";
    private const int Created = 201;
    private const int Accepted = 202;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);

    /// <summary>The seventeenth candidate must finish while all earlier candidates remain queued with exact unchanged failed evidence.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_FullPageOfPoisonedJobs_When_ActualWorkerPolls_Then_NextHealthyCandidateIsNotStarved(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await SeedAsync(test);
        await using var setup = test.Scope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var jobs = await database.Set<CatalogueDeletion>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(test.Token);
        var poisoned = jobs.Take(PageSize).Select(row => row.AdmissionAuditId).ToArray();
        Assert.Equal(PageSize, await database.Set<SecurityAuditIntent>().Where(row => poisoned.Contains(row.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Outcome, InvalidOutcome), test.Token));
        var original = jobs.Take(PageSize).Select(CatalogueDeletionSnapshot.Capture).ToArray();
        var badAudits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => poisoned.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync(test.Token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using var host = GrantWorkerTests.CreateHost(provider, test.Scenario.Account.Journey.Lease.RuntimeConnection, test.Clock);
        await host.StartAsync(deadline.Token);
        await CatalogueDeletionRestartTests.ObserveAsync(test, jobs[^1].Id, deadline.Token);
        await host.StopAsync(deadline.Token);
        await CatalogueDeletionTest.RequireTerminalAsync(test, jobs[^1].Id, CatalogueDeletionFailure.None);
        foreach (var snapshot in original)
        {
            Assert.Equal(snapshot, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, snapshot.Id)));
        }
        Assert.Equal(CatalogueMutationEvidence.Hash(badAudits), CatalogueMutationEvidence.Hash(await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => poisoned.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync(test.Token)));
        Assert.Equal(PageSize, await database.Set<ApiAudience>().CountAsync(test.Token));
    }

    /// <summary>Admits every job through real current-authority production operations before deliberately corrupting only the selected retained audit evidence.</summary>
    private static async Task SeedAsync(ApplicationMutationFixture test)
    {
        for (var index = 0; index < Candidates; index++)
        {
            var created = await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Create(new AudienceInput(Resource + index.ToString(CultureInfo.InvariantCulture), Display, Owner, true))!);
            Assert.Equal(Created, created.Status);
            var intent = CatalogueDeleteIntent.Create(CatalogueResource.Audience, created.Audience!.Id, created.Audience.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
            Assert.Equal(Accepted, (await CatalogueDeletionTest.AdmitAsync(test, intent)).Status);
        }
    }
}
