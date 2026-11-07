using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires shipped catalogue UI, actual issuer ceremonies, real API/BFF and hosted deletion on both providers and retained browser engines.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class CatalogueBrowserTests(BootstrapFixture fixture)
{
    private const int InitialRevision = 1;
    private const int JobCount = 2;
    private const int TerminalRevision = 2;
    private const string Resource = "browser-orders-api";
    private const string Name = "browser.orders.read";
    private const string Deleted = "CatalogueDeleted";
    private const string Admitted = "CatalogueDeletionAdmitted";

    /// <summary>Creates and edits real catalogue entries, preserves searched selection, blocks referenced deletion and observes durable completed jobs with exact independent storage.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_CurrentAdministrator_When_CatalogueBrowserJourneyRuns_Then_RealAccessiblePolicyAndAuditedDeletionComplete(string provider) =>
        AdministratorBrowserEvidence.RunAsync(() => RunAsync(provider));

    /// <summary>Joins the browser to genuine migrated hosts and production Worker composition while preserving all preexisting native scope rows.</summary>
    private async Task RunAsync(string provider)
    {
        var clock = new FactorClock(TimeProvider.System);
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, clock: clock);
        await using var browser = await BrowserSession.CreateAsync();
        var page = await browser.Context.NewPageAsync();
        await ApplicationAccessFixture.SignInAsync(environment, page);
        await using var observer = environment.Issuer.Account.CreateScope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var original = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Name, row.Resources }).ToArrayAsync(token);
        await CatalogueBrowserJourney.CreateAudienceAsync(page, environment.Profile.ClientUri);
        var audience = await database.Set<ApiAudience>().AsNoTracking().SingleAsync(row => row.Resource == Resource, token);
        Assert.Equal(InitialRevision, audience.Revision);
        await CatalogueBrowserJourney.CreateScopeAsync(page, environment.Profile.ClientUri, audience.Id);
        var scope = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().SingleAsync(row => row.Name == Name, token);
        var policy = await database.Set<ScopePolicy>().AsNoTracking().SingleAsync(row => row.ScopeId == scope.Id, token);
        Assert.Equal(Resource, Assert.Single(System.Text.Json.JsonSerializer.Deserialize<string[]>(scope.Resources!)!));
        Assert.Equal(InitialRevision, policy.Revision);
        await CatalogueBrowserJourney.RequireReferencedAsync(page, environment.Profile.ClientUri);
        await using var worker = GrantWorkerTests.CreateHost(provider, environment.Issuer.Account.Journey.Lease.RuntimeConnection, clock);
        await worker.StartAsync(token);
        await CatalogueBrowserJourney.DeleteAsync(page, environment.Profile.ClientUri, scope: true);
        await CatalogueBrowserJourney.DeleteAsync(page, environment.Profile.ClientUri, scope: false);
        await worker.StopAsync(token);
        await RequireAsync(database, audience.Id, scope.Id!, original);
        await WorkspacePresentation.RequireStorageAsync(page);
    }

    /// <summary>Requires original native rows, exact terminal target bindings and linked admission/completion audit pairs independently of UI text.</summary>
    private static async Task RequireAsync<T>(PlatformDbContext database, string audience, string scope, T[] original)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.False(await database.Set<ApiAudience>().AnyAsync(token));
        Assert.False(await database.Set<ScopePolicy>().AnyAsync(token));
        var current = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Name, row.Resources }).ToArrayAsync(token);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(current));
        var jobs = await database.Set<CatalogueDeletion>().AsNoTracking().ToArrayAsync(token);
        Assert.Equal(JobCount, jobs.Length);
        Assert.Equal(new[] { audience, scope }.Order(), jobs.Select(row => row.TargetId).Order());
        foreach (var job in jobs)
        {
            Assert.Equal(CatalogueDeletionState.Succeeded, job.State);
            Assert.Equal(CatalogueDeletionFailure.None, job.Failure);
            Assert.Equal(TerminalRevision, job.Revision);
            var admission = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Id == job.AdmissionAuditId, token);
            var terminal = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Id == job.CompletionAuditId, token);
            Assert.Equal(Admitted, admission.Event);
            Assert.Equal(Deleted, terminal.Event);
            Assert.Equal(admission.CorrelationId, terminal.RelatedCorrelationId);
            Assert.Equal(job.TargetId, terminal.TargetId);
            Assert.Equal(job.ActorId, terminal.ActorId);
        }
    }
}
