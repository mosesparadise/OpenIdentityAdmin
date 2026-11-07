using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Admin.Api.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Exercises real deletion admission and worker boundaries with independent durable-state observations.</summary>
internal static class CatalogueDeletionTest
{
    private const int CreatedStatus = 201;
    private const int TerminalRevision = 2;
    private const int AuditCount = 2;
    private const string UserTarget = "User";
    private const string ScopeTarget = "Scope";
    private const string AudienceTarget = "Audience";
    private const string AcceptedOutcome = "Accepted";
    private const string RejectedOutcome = "Rejected";
    private const string Resource = "deletion-api";
    private const string Display = "Deletion example";
    private const string Owner = "Operations";
    private const string Email = "email";
    private const string Identity = "identity";
    private const string Consent = "Share email";
    private const string GuidFormat = "D";
    private const string Admitted = "CatalogueDeletionAdmitted";
    private const string Deleted = "CatalogueDeleted";
    private const string Denied = "CatalogueDeletionRejected";

    /// <summary>Creates either actual native scope/policy or product audience through the production coordinator.</summary>
    internal static async Task<CatalogueDeleteIntent> CreateAsync(ApplicationMutationFixture test, bool scope = false)
    {
        var result = await CatalogueMutationTests.ExecuteAsync(test, scope
            ? CatalogueIntent.Create(new ScopeInput(Email, Display, Identity, true, Consent, null, Email))!
            : CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!);
        Assert.Equal(CreatedStatus, result.Status);
        var id = result.Scope?.Id ?? result.Audience!.Id;
        var version = result.Scope?.Version ?? result.Audience!.Version;
        return CatalogueDeleteIntent.Create(scope ? CatalogueResource.Scope : CatalogueResource.Audience,
            id, version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
    }

    /// <summary>Invokes one genuine current-authority request with its original budget and no authorization override.</summary>
    internal static async Task<CatalogueDeleteResult> AdmitAsync(ApplicationMutationFixture test, CatalogueDeleteIntent intent, ServiceProvider? peer = null, Guid? correlation = null)
    {
        var services = peer ?? test.Api;
        using var budget = new ApplicationReadBudget(test.Clock, test.Token);
        return await CatalogueDeleteOperation.ExecuteAsync(services.GetRequiredService<IServiceScopeFactory>(), test.Identity,
            intent, services.GetRequiredService<ApiResourceProfile>(), test.Clock, budget, correlation ?? Guid.NewGuid());
    }

    /// <summary>Executes one worker attempt on an independent root when supplied; no retained admission receipt is reused.</summary>
    internal static Task<bool> ProcessAsync(ApplicationMutationFixture test, Guid id, ServiceProvider? peer = null) =>
        CatalogueDeletionProcessor.ExecuteAsync((peer ?? test.Api).GetRequiredService<IServiceScopeFactory>(), id, test.Clock, test.Token);

    /// <summary>Reloads the entire durable job without retaining tracked entities across any mutation.</summary>
    internal static async Task<CatalogueDeletion> ReadAsync(ApplicationMutationFixture test, Guid id)
    {
        await using var observer = test.Scope();
        return await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<CatalogueDeletion>()
            .AsNoTracking().SingleAsync(row => row.Id == id, test.Token);
    }

    /// <summary>Checks exact audit actor/target/time/correlation, one terminal transition and independent target presence.</summary>
    internal static async Task RequireTerminalAsync(ApplicationMutationFixture test, Guid id, CatalogueDeletionFailure failure)
    {
        var job = await ReadAsync(test, id);
        Assert.Equal(failure == CatalogueDeletionFailure.None ? CatalogueDeletionState.Succeeded : CatalogueDeletionState.Rejected, job.State);
        Assert.Equal(failure, job.Failure);
        Assert.Equal(TerminalRevision, job.Revision);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Id == job.AdmissionAuditId || row.Id == job.CompletionAuditId).ToArrayAsync(test.Token);
        Assert.Equal(AuditCount, audits.Length);
        var admitted = Assert.Single(audits, row => row.Event == Admitted);
        var terminal = Assert.Single(audits, row => row.Event == (failure == CatalogueDeletionFailure.None ? Deleted : Denied));
        Assert.Equal(admitted.CorrelationId, terminal.RelatedCorrelationId);
        Assert.Null(admitted.RelatedCorrelationId);
        Assert.NotEqual(Guid.Empty, terminal.CorrelationId);
        Assert.Equal(job.CreatedAt, admitted.OccurredAt);
        Assert.Equal(job.ObservedAt, terminal.OccurredAt);
        Assert.All(audits, row => { Assert.Equal(test.Identity.Subject, row.ActorId); Assert.Equal(UserTarget, row.ActorType); Assert.Equal(job.TargetId, row.TargetId); Assert.Equal(job.Kind == CatalogueVersion.Scope ? ScopeTarget : AudienceTarget, row.TargetType); });
        Assert.Equal(AcceptedOutcome, admitted.Outcome);
        Assert.Equal(failure == CatalogueDeletionFailure.None ? AcceptedOutcome : RejectedOutcome, terminal.Outcome);
        var present = job.Kind == CatalogueVersion.Scope
            ? await database.Set<ScopePolicy>().AnyAsync(row => row.ScopeId == job.TargetId, test.Token)
            : await database.Set<ApiAudience>().AnyAsync(row => row.Id == job.TargetId, test.Token);
        Assert.Equal(failure != CatalogueDeletionFailure.None, present);
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var original = CatalogueDeletionSnapshot.Capture(job);
        Assert.False(await ProcessAsync(test, id));
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
        Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await ReadAsync(test, id)));
    }
}
