using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires independently observed rollback after genuine destination/audit persistence and source/catalogue substitution.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneAtomicityTests(BootstrapFixture fixture)
{
    private const string Email = "email";
    private const string Orders = "orders.read";
    private const string Display = "Atomic clone";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Destination = "atomic-clone";

    /// <summary>Persisted corruption cannot commit a clone or mutate its source, even when all manager saves already succeeded.</summary>
    [Theory]
    [InlineData("PostgreSql", ApplicationCloneDefect.Provenance, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationCloneDefect.SourcePolicy, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationCloneDefect.SourceNative, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationCloneDefect.Catalogue, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", ApplicationCloneDefect.Provenance, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationCloneDefect.SourcePolicy, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationCloneDefect.SourceNative, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationCloneDefect.Catalogue, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedClone_When_SourceCatalogueOrProvenanceIsSubstituted_Then_AllRowsRollBack(string provider, ApplicationCloneDefect defect)
    {
        var fault = new ApplicationCloneSaveFault(defect);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var selected = await ApplicationScopeEvidence.SeedAsync(test);
        var source = await test.ExecuteAsync(test.Create());
        var assigned = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(source.Application!.Id, source.Version!, new([Email, Orders]))!);
        Assert.Equal(200, assigned.Status);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id);
        var catalogue = await CatalogueMutationEvidence.SnapshotAsync(test);
        fault.ScopeId = selected[1].Id;
        fault.Armed = true;
        var command = Clone(test, source.Application.Id, assigned.Version!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.ExecuteAsync(command));
        Assert.True(fault.Fired);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        Assert.Equal(catalogue, await CatalogueMutationEvidence.SnapshotAsync(test));
        await RequireAbsentAsync(test);
        fault.Armed = false;
        Assert.Equal(201, (await test.ExecuteAsync(command)).Status);
    }

    /// <summary>Failure before audit persistence rolls back earlier destination/hash/policy saves and leaves the original source/version reusable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NewNativeClone_When_AuditSaveFails_Then_NoDestinationCredentialOrHistoryCommits(string provider)
    {
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.AuditFailure);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var source = await test.ExecuteAsync(test.Create());
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        fault.Armed = true;
        var command = Clone(test, source.Application.Id, source.Version!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.ExecuteAsync(command));
        Assert.True(fault.Fired);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        await RequireAbsentAsync(test);
        fault.Armed = false;
        Assert.Equal(201, (await test.ExecuteAsync(command)).Status);
    }

    /// <summary>Supplies a causal source-verification witness: actual post-audit policy corruption must fail before commit and preserve every prior row.</summary>
    [Trait("CriticalDecision", "application-clone-source-verification")]
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CloneSourcePolicyCorruption_When_FinalVerificationRuns_Then_NoChangedSourceOrDestinationCommits(string provider)
    {
        const string Witness = "application-clone-source-verification-bypass";
        var fault = new ApplicationCloneSaveFault(ApplicationCloneDefect.SourcePolicy);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var source = await test.ExecuteAsync(test.Create());
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        fault.Armed = true;
        var error = await Record.ExceptionAsync(() => test.ExecuteAsync(Clone(test, source.Application.Id, source.Version!)));
        Assert.True(fault.Fired);
        Assert.True(error is InvalidOperationException, Witness);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        await RequireAbsentAsync(test);
    }

    /// <summary>Observes absence independently from discarded command output after the owning transaction has rolled back.</summary>
    private static async Task RequireAbsentAsync(ApplicationMutationFixture test)
    {
        await using var scope = test.Scope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(Destination, test.Token));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationCloneAuditDetail>().AsNoTracking().ToArrayAsync(test.Token));
    }

    /// <summary>Uses an independently chosen destination and future expiry while retaining the exact source precondition.</summary>
    internal static ApplicationMutationCommand Clone(ApplicationMutationFixture test, string id, string version, string client = Destination) =>
        ApplicationMutationCommand.Clone(id, version, new(true, client, Display,
            test.Clock.Now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture)), test.Clock.Now)!;
}
