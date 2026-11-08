using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Independently witnesses critical deletion outcomes for actual behavioral mutation controls on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueMutationControlTests(BootstrapFixture fixture)
{
    private const string ExpiryMarker = "catalogue-original-approval-bypass";
    private const string ReferenceMarker = "catalogue-retained-reference-bypass";
    private const string CommitMarker = "catalogue-terminal-commit-bypass";
    private const string Name = "email";
    private const int TargetSnapshots = 3;

    /// <summary>Equality at the retained original approval deadline rejects deletion without touching the native scope or policy.</summary>
    [Theory, Trait("CriticalDecision", "catalogue-original-approval")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OriginalDeletionApproval_When_ExactDeadlineIsReached_Then_NativeScopeSurvivesExpiredDecision(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var job = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var original = await CatalogueDeletionTest.ReadAsync(test, job.Id);
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.ApprovalUntil);
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, job.Id));
        var current = await CatalogueDeletionTest.ReadAsync(test, job.Id);
        if (current.State == CatalogueDeletionState.Succeeded)
        {
            await RequireRemovedAsync(test, intent.Id, job.Id);
            Assert.Fail(ExpiryMarker);
        }
        Assert.Equal(original.ApprovalUntil, current.ApprovalUntil);
        Assert.Equal(before.Take(TargetSnapshots), (await CatalogueMutationEvidence.SnapshotAsync(test)).Take(TargetSnapshots));
        await CatalogueDeletionTest.RequireTerminalAsync(test, job.Id, CatalogueDeletionFailure.Expired);
    }

    /// <summary>A retained revoked native authorization introduced after admission remains a reference that prevents scope removal.</summary>
    [Theory, Trait("CriticalDecision", "catalogue-retained-reference")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedScopeDeletion_When_RetainedRevokedGrantAppears_Then_ReferencePreventsNativeRemoval(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var job = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        await using var observer = test.Scope();
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            Status = OpenIddictConstants.Statuses.Revoked,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        };
        descriptor.Scopes.Add(Name);
        var manager = observer.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await manager.CreateAsync(descriptor, test.Token);
        var id = await manager.GetIdAsync(authorization, test.Token);
        var persisted = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .AsNoTracking().SingleAsync(row => row.Id == id, test.Token);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, persisted.Status);
        Assert.Equal(Name, Assert.Single(System.Text.Json.JsonSerializer.Deserialize<string[]>(persisted.Scopes!)!));
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, job.Id));
        var current = await CatalogueDeletionTest.ReadAsync(test, job.Id);
        if (current.State == CatalogueDeletionState.Succeeded)
        {
            await RequireRemovedAsync(test, intent.Id, job.Id);
            Assert.Fail(ReferenceMarker);
        }
        Assert.Equal(before.Take(TargetSnapshots), (await CatalogueMutationEvidence.SnapshotAsync(test)).Take(TargetSnapshots));
        await CatalogueDeletionTest.RequireTerminalAsync(test, job.Id, CatalogueDeletionFailure.Referenced);
    }

    /// <summary>Successful worker return requires independently visible native removal, durable terminal status and paired audit after actual cleanup.</summary>
    [Theory, Trait("CriticalDecision", "catalogue-terminal-commit")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UnreferencedScope_When_DeletionReturnsSuccess_Then_TerminalNativeAndAuditStateIsCommitted(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var job = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, job.Id));
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, job.Id));
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var current = await database.Set<CatalogueDeletion>().AsNoTracking().SingleAsync(test.Token);
        var exists = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Id == intent.Id, test.Token);
        if (current.State == CatalogueDeletionState.Queued)
        {
            Assert.True(exists);
            Assert.Equal(original, CatalogueDeletionSnapshot.Capture(current));
            Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
            Assert.Fail(CommitMarker);
        }
        Assert.False(exists);
        await CatalogueDeletionTest.RequireTerminalAsync(test, job.Id, CatalogueDeletionFailure.None);
    }

    /// <summary>Proves an actual fully committed violating deletion before crediting the corresponding behavioral mutation marker.</summary>
    private static async Task RequireRemovedAsync(ApplicationMutationFixture test, string target, Guid job)
    {
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Id == target, test.Token));
        Assert.False(await database.Set<ScopePolicy>().AsNoTracking().AnyAsync(row => row.ScopeId == target, test.Token));
        await CatalogueDeletionTest.RequireTerminalAsync(test, job, CatalogueDeletionFailure.None);
    }
}
