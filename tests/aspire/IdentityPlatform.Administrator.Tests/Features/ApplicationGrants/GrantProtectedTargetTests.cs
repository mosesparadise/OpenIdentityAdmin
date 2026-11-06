using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Isolates persisted administrator target protection from authentication, which would otherwise reject the deliberately changed profile before the target guard runs.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantProtectedTargetTests(BootstrapFixture fixture)
{
    private const string Administrator = "identity-admin-bff";
    private const string Pilot = "identityplatform-pilot";

    /// <summary>An otherwise eligible ordinary-named target remains protected by administrator identity; restoration permits genuine current-authority admission of the same intent.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OrdinaryManagedTarget_When_PersistedAdministratorIdentityBindsIt_Then_GrantProtectionDeniesWithoutNameOrAuthenticationMasking(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, 1, 1);
        var intent = test.Intent();
        await RequireOrdinaryNamesAsync(test);
        var original = await SnapshotAsync(test);
        Assert.True(await ReadTargetAsync(test));
        Assert.Equal(original, await SnapshotAsync(test));
        var prior = await BindAsync(test, test.Id);
        try
        {
            var bound = await SnapshotAsync(test);
            var failure = await Assert.ThrowsAsync<ApplicationMutationRejected>(() => ReadTargetAsync(test));
            Assert.Equal(ApplicationMutationFailure.Forbidden, failure.Failure);
            Assert.Equal(bound, await SnapshotAsync(test));
            await RequireNoAdmissionAsync(test);
        }
        finally { await BindAsync(test, prior); }
        Assert.Equal(original, await SnapshotAsync(test));
        var admitted = await test.AdmitAsync(intent);
        Assert.Equal("queued", admitted.State);
        Assert.Equal("0", admitted.AuthorizationCount);
        Assert.Equal("0", admitted.TokenCount);
        var history = await GrantAuditWitness.ReadAsync(test, admitted.Id);
        history.Require();
        Assert.Single(history.Approvals);
    }

    /// <summary>Runs the production target reader under genuine owned transaction/lock lifetime, without fabricating an API bearer or authorization receipt.</summary>
    private static async Task<bool> ReadTargetAsync(GrantTestContext test)
    {
        var request = new ProtocolStorageRequest(test.Fixture.Clock, test.Token);
        try
        {
            return await request.RunAsync(test.Fixture.Api.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, test.Fixture.Identity.Subject, token);
                var target = await GrantTarget.ReadAsync(scope, test.Id, token);
                GrantTarget.RequireDisabled(target.Policy, test.Application.Version!);
                await target.VerifyAsync(scope, token);
                return true;
            });
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Alters only the actual singleton's application reference, retaining every real foreign key and restoring the prior identity in the caller's finally.</summary>
    private static async Task<string> BindAsync(GrantTestContext test, string application)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var profile = await database.Set<AdministratorProtocolProfile>().SingleAsync(test.Token);
        var previous = profile.ApplicationId;
        profile.ApplicationId = application;
        await database.SaveChangesAsync(test.Token);
        return previous;
    }

    /// <summary>Explicitly excludes reserved client-name protection as the reason for denying the later persisted-identity test.</summary>
    private static async Task RequireOrdinaryNamesAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var row = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking().SingleAsync(value => value.Id == test.Id, test.Token);
        Assert.False(string.Equals(row.ClientId, Administrator, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals(row.ClientId, Pilot, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(row.DisplayName));
    }

    /// <summary>Hashes full native lifecycle/configuration, policy/audit and the entire persisted administrator profile from fresh observations.</summary>
    private static async Task<string> SnapshotAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var application = await ApplicationUpdateSnapshot.ReadAsync(database, test.Id, test.Token);
        var native = await ManagedJourneyEvidence.NativeAsync(database);
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(test.Token);
        return ApplicationUpdateSnapshot.Hash(new
        {
            application.Digest,
            native,
            profile
        });
    }

    /// <summary>No target denial may create a job, approval or progress audit; the final authorized control is checked separately.</summary>
    private static async Task RequireNoAdmissionAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<ApplicationGrantJob>().AnyAsync(test.Token));
        Assert.False(await database.Set<ApplicationGrantApproval>().AnyAsync(test.Token));
        Assert.False(await database.Set<ApplicationGrantAudit>().AnyAsync(test.Token));
    }
}
