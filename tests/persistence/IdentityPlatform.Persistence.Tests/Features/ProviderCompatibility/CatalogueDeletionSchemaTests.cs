using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises durable deletion constraints, exact audit references and competing revisions on both real engines.</summary>
public sealed class CatalogueDeletionSchemaTests
{
    private const string Postgres = "PostgreSql";
    private const string InvalidKind = "other";
    private const int InvalidState = 9;
    private const long BeforeCreation = CatalogueSchemaRows.Created - 1;
    private const long ExpiredWindow = CatalogueSchemaRows.ApprovalUntil + 1;
    private static readonly Action<CatalogueDeletion>[] InvalidRows =
    [
        row => row.Kind = InvalidKind, row => row.TargetRevision = 0, row => row.Revision = 0,
        row => row.AuthenticationGeneration = 0, row => row.FactorGeneration = 0, row => row.EnrollmentRevision = -1,
        row => row.FactorVerifiedAt = -1, row => row.CreatedAt = BeforeCreation, row => row.ObservedAt = BeforeCreation,
        row => row.ApprovalUntil = CatalogueSchemaRows.Created, row => row.ApprovalUntil = ExpiredWindow,
        row => row.State = (CatalogueDeletionState)InvalidState, row => row.Failure = (CatalogueDeletionFailure)InvalidState,
        row => row.Failure = CatalogueDeletionFailure.Stale, row => row.State = CatalogueDeletionState.Succeeded,
        row => row.State = CatalogueDeletionState.Rejected, row => row.CompletionAuditId = row.AdmissionAuditId,
        row => row.AdmissionAuditId = Guid.NewGuid()
    ];

    /// <summary>Rejects malformed authority, time, target, state and audit relationships without leaving either half of a failed write.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_InvalidDeletionEvidence_When_StoredDirectly_Then_ConstraintsRejectAndRollbackEachWrite(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        foreach (var invalidate in InvalidRows)
        {
            await using var scope = fixture.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            invalidate(CatalogueSchemaRows.Add(database));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
        }
        await using var observer = fixture.Scope();
        var actual = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await actual.Set<CatalogueDeletion>().ToListAsync(fixture.Token));
        Assert.Empty(await actual.Set<SecurityAuditIntent>().ToListAsync(fixture.Token));
    }

    /// <summary>Preserves maximum-width approval, unique request identity and independent terminal audit after a competing writer loses.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedDeletion_When_TwoWritersComplete_Then_OnlyFirstOutcomeAndAuditCommit(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var first = fixture.Scope();
        var one = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var original = CatalogueSchemaRows.Add(one);
        await one.SaveChangesAsync(fixture.Token);
        await RequireKeyConflictAsync(fixture, original.ScopedKeyHash);
        await using var second = fixture.Scope();
        var two = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stale = await two.Set<CatalogueDeletion>().SingleAsync(fixture.Token);
        Assert.Equal(CatalogueSchemaRows.TargetLength, stale.TargetId.Length);
        Assert.Equal(CatalogueSchemaRows.ActorLength, stale.ActorId.Length);
        Assert.Equal(CatalogueSchemaRows.StampLength, stale.SecurityStamp.Length);
        var success = Complete(one, original, CatalogueDeletionFailure.None);
        await one.SaveChangesAsync(fixture.Token);
        var rejected = Complete(two, stale, CatalogueDeletionFailure.Stale);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => two.SaveChangesAsync(fixture.Token));
        await using var observer = fixture.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actual = await database.Set<CatalogueDeletion>().SingleAsync(fixture.Token);
        Assert.Equal(CatalogueDeletionState.Succeeded, actual.State);
        Assert.Equal(success.Id, actual.CompletionAuditId);
        Assert.False(await database.Set<SecurityAuditIntent>().AnyAsync(row => row.Id == rejected.Id, fixture.Token));
        database.Remove(await database.Set<SecurityAuditIntent>().SingleAsync(row => row.Id == success.Id, fixture.Token));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
    }

    /// <summary>Checks both successful and rejected terminal combinations and refuses aliasing admission as completion evidence.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DeletionTerminalState_When_AuditAndFailureAreBound_Then_OnlyConsistentIndependentEvidencePersists(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = CatalogueSchemaRows.Add(database);
        Complete(database, row, CatalogueDeletionFailure.Referenced);
        await database.SaveChangesAsync(fixture.Token);
        row.CompletionAuditId = row.AdmissionAuditId;
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
        await using var observer = fixture.Scope();
        var actual = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<CatalogueDeletion>().SingleAsync(fixture.Token);
        Assert.Equal(CatalogueDeletionState.Rejected, actual.State);
        Assert.Equal(CatalogueDeletionFailure.Referenced, actual.Failure);
        Assert.NotEqual(actual.AdmissionAuditId, actual.CompletionAuditId);
    }

    /// <summary>Creates independently observable terminal evidence in the same transaction as the revision mutation.</summary>
    private static SecurityAuditIntent Complete(PlatformDbContext database, CatalogueDeletion row, CatalogueDeletionFailure failure)
    {
        var audit = CatalogueSchemaRows.Audit();
        database.Add(audit);
        row.CompletionAuditId = audit.Id;
        row.State = failure == CatalogueDeletionFailure.None ? CatalogueDeletionState.Succeeded : CatalogueDeletionState.Rejected;
        row.Failure = failure;
        row.Revision = CatalogueSchemaRows.NextRevision;
        return audit;
    }

    /// <summary>Uses an independent context to prove duplicate scoped idempotency identities are rejected by the actual unique index.</summary>
    private static async Task RequireKeyConflictAsync(ManagedSchemaFixture fixture, string key)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        CatalogueSchemaRows.Add(database).ScopedKeyHash = key;
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
    }
}
