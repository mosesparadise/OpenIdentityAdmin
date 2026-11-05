using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves current persisted lifecycle permission and component MFA boundaries independently from write and credential grants.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleAuthorityTests(BootstrapFixture fixture)
{
    private const int Updated = 200;
    private const string Bypass = "application-lifecycle-permission-bypass";
    private const string Maintenance = "maintenance";
    private const string AllowedEdit = "Ordinary authorized edit";
    private const string ForbiddenEdit = "Forbidden edit";

    private const string LifecyclePermission = "applications.disable";
    private const string WritePermission = "applications.write";
    private const string CredentialPermission = "applications.credentials";
    private const int MfaSeconds = 300;

    /// <summary>Removing only lifecycle permission or reaching exact MFA expiry denies both state transitions without any persisted side effect.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql", "CriticalDecision", "application-lifecycle-permission"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer", "CriticalDecision", "application-lifecycle-permission"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_MissingLifecycleGrantOrExpiredMfa_When_TransitionRequested_Then_DeniedWithoutMutationWhileOrdinaryEditWorks(string provider, bool enabled, bool expireMfa)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled);
        if (expireMfa)
        {
            await using var scope = test.Scope();
            var verified = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds);
        }
        else
        {
            await RemoveAsync(test, LifecyclePermission);
        }
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var denied = await test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), enabled)!);
        Assert.False(denied.Status == Updated, Bypass);
        Assert.Equal(403, denied.Status);
        Assert.Null(denied.Application);
        Assert.Null(denied.Credential);
        Assert.Null(denied.Version);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
        Assert.Equal(200, (await test.ExecuteAsync(ApplicationMutationCommand.Edit(current.Application.Id, current.Version!,
            new(AllowedEdit, [ApplicationMutationFixture.Redirect]))!)).Status);
    }

    /// <summary>Lifecycle authority alone permits state transitions but never grants ordinary edit or credential replacement.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OnlyLifecycleGrant_When_DisableAndEnableRequested_Then_BothWorkWithoutGrantingOtherMutations(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled: false);
        await RemoveAsync(test, WritePermission);
        await RemoveAsync(test, CredentialPermission);
        foreach (var enabled in new[] { false, true })
        {
            current = await test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(current.Application!.Id, current.Version!, new(Maintenance, true), enabled)!);
            Assert.Equal(200, current.Status);
            var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
            Assert.Equal(403, (await test.ExecuteAsync(test.Replace(current))).Status);
            Assert.Equal(403, (await test.ExecuteAsync(ApplicationMutationCommand.Edit(current.Application.Id, current.Version!, new(ForbiddenEdit, [ApplicationMutationFixture.Redirect]))!)).Status);
            Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
        }
    }

    /// <summary>Removes only the named current persisted grant in this isolated disposable fixture.</summary>
    private static async Task RemoveAsync(ApplicationMutationFixture test, string permission)
    {
        await using var scope = test.Scope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(test.Token) > 0);
    }
}
