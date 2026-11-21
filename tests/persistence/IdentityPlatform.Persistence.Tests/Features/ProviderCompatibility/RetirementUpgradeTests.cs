using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Proves the immediate populated clone predecessor upgrades without retirement adoption, native deletion or history changes.</summary>
public sealed class RetirementUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string Active = "retirement-upgrade-enabled";
    private const string Disabled = "retirement-upgrade-disabled";
    private const string Subject = "retirement-upgrade-subject";
    private const string Event = "ApplicationCloned";
    private const string User = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private const int SecretBytes = 32;
    private const long Revision = 7;
    private const int PolicyCount = 3;
    private const int HistoricalRevision = 3;
    private const int HistoricalCount = 7;

    /// <summary>Runs the real Migrator twice and calibrates an exact physical witness including prior grant progress and nonempty clone provenance.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedClonePredecessor_When_RetirementMigratesTwice_Then_ExistingPoliciesNativeCredentialsAndAllHistoryRemainExact(string provider)
    {
        await using var test = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ? CloneUpgradeWitness.PostgresMigration : CloneUpgradeWitness.SqlMigration);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        await SeedAsync(test, secret);
        var before = await test.SnapshotAsync(provider);
        Assert.NotEmpty(before[CloneUpgradeWitness.Query]);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        var after = await test.SnapshotAsync(provider);
        RetirementUpgradeWitness.Require(provider, before, after);
        RetirementUpgradeWitness.RejectDefects(provider, before, after);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        RetirementUpgradeWitness.Require(provider, before, await test.SnapshotAsync(provider));
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policies = await database.Set<ApplicationPolicy>().AsNoTracking().ToArrayAsync(test.Token);
        Assert.Equal(PolicyCount, policies.Length);
        Assert.All(policies, policy => Assert.False(policy.Retired));
        Assert.Contains(policies, policy => policy.Enabled);
        Assert.Contains(policies, policy => !policy.Enabled);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(Active, test.Token);
        Assert.NotNull(application);
        Assert.True(await manager.ValidateClientSecretAsync(application, secret, test.Token));
        Assert.Empty(await database.Set<ApplicationRetirement>().AsNoTracking().ToArrayAsync(test.Token));
        await ProfileStorageRoundTrip.RequireAsync(test, provider, after);
    }

    /// <summary>Uses native managers plus explicit predecessor policy columns, including actual prior secret material and both enabled states.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture test, string secret)
    {
        await using var scope = test.Scope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        string? source = null;
        foreach (var client in new[] { Active, Disabled })
        {
            var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = client,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ClientSecret = secret
            }, test.Token);
            var id = (await applications.GetIdAsync(application, test.Token))!;
            var policy = ManagedSchemaRows.Policy();
            policy.ApplicationId = id;
            policy.Enabled = client == Active;
            policy.Revision = Revision;
            await RetirementPredecessorRows.InsertAsync(database, policy, test.Token);
            await SeedNativeAsync(test, services, id);
            if (source is null)
            {
                source = id;
            }
            else
            {
                SeedClone(database, source, id);
            }
        }
        var history = GrantSchemaRows.Add(database);
        await RetirementPredecessorRows.InsertAsync(database, history.Policy, test.Token);
        history.Job.State = IdentityPlatform.Core.Features.ApplicationGrants.GrantJobState.Processing;
        history.Job.Revision = HistoricalRevision;
        history.Job.TokenCount = HistoricalCount;
        history.Detail.State = history.Job.State;
        history.Detail.JobRevision = HistoricalRevision;
        history.Detail.TokenCount = HistoricalCount;
        await database.SaveChangesAsync(test.Token);
    }

    /// <summary>Preserves nonempty source/destination provenance in the immediate predecessor instead of silently projecting it away.</summary>
    private static void SeedClone(PlatformDbContext database, string source, string destination)
    {
        var audit = new SecurityAuditIntent
        {
            Event = Event,
            ActorType = User,
            ActorId = Subject,
            TargetType = Target,
            TargetId = destination,
            Outcome = Accepted,
            OccurredAt = ManagedSchemaRows.Created
        };
        database.Add(audit);
        database.Add(new ApplicationCloneAuditDetail { AuditId = audit.Id, SourceApplicationId = source, SourceVersion = ApplicationVersion.Create(source, Revision) });
    }

    /// <summary>Populates native retained authorization and token associations whose exact physical values must survive both upgrades.</summary>
    private static async Task SeedNativeAsync(ManagedSchemaFixture test, IServiceProvider services, string id)
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, test.Token);
        await services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = await authorizations.GetIdAsync(authorization, test.Token),
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
        }, test.Token);
    }
}
