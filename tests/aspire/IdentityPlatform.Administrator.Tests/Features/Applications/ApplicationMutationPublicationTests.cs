using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves the actual mutation coordinator withholds credentials on cancellation and ambiguous post-commit cleanup failure.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationMutationPublicationTests(BootstrapFixture fixture)
{
    /// <summary>Caller cancellation after native persistence forces rollback and cannot return or retry a generated secret.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CancellationAfterNativeSave_When_CreateRuns_Then_NoResultOrPersistedMutationSurvives(string provider)
    {
        using var cancellation = new CancellationTokenSource();
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.Cancelled) { Advance = cancellation.Cancel };
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        fault.Armed = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.ExecuteWithCancellationAsync(test.Create(), cancellation.Token));
        Assert.True(fault.Fired);
        await using var scope = test.Scope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(ApplicationMutationFixture.Client, test.Token));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().ToArrayAsync(test.Token));
        Assert.Equal(0, await test.AuditsAsync());
    }

    /// <summary>A confirmed commit followed by actual child disposal failure remains ambiguous to the caller and creates exactly one durable mutation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedMutation_When_ChildCleanupFails_Then_CredentialOutputIsWithheldWithoutImplicitRetry(string provider)
    {
        var fault = new ApplicationMutationCleanupFault();
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, cleanup: fault);
        fault.Armed = true;
        var failure = await Record.ExceptionAsync(() => test.ExecuteAsync(test.Create()));
        Assert.True(fault.Contains(failure));
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        await using var scope = test.Scope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(ApplicationMutationFixture.Client, test.Token));
        Assert.Single(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().ToArrayAsync(test.Token));
        Assert.Equal(1, await test.AuditsAsync());
    }
}
