using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Requires native assignment/resource authority and current full metadata despite warm manager caches and unchanged product revisions.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopePolicyTests(BootstrapFixture fixture)
{
    private const int AssignedRevision = 2;
    private const string Orders = "orders.read";
    private const string OriginalPermission = "scp:email";
    private const string OtherPermission = "scp:other";
    private const string MissingResource = "[\"missing-resource\"]";
    private const string CurrentPolicyBypass = "managed-current-scope-policy-bypass";
    private const string Changed = " changed";
    private const string IdentityKind = "identity";

    /// <summary>Independent persisted changes invalidate prior decisions; matching revisions alone cannot keep stale metadata or native permissions alive.</summary>
    [Theory]
    [Trait("CriticalDecision", "managed-current-scope-policy")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CapturedCataloguePolicy_When_CurrentNativeOrMetadataDrifts_Then_OriginalDecisionCannotBeReused(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var id = await ManagedScopeTest.SeedAsync(test);
        var original = await ManagedScopeTest.CaptureAsync(test);
        Assert.Equal(id, original.ApplicationId);
        Assert.Equal(AssignedRevision, original.ApplicationRevision);
        Assert.Equal(["email", "openid", Orders], original.Requested);
        Assert.Equal(["openid"], original.Granted);
        Assert.Equal("orders-api", original.Audience!.Resource);
        Assert.Equal(AssignedRevision, original.Scopes.Length);
        Assert.Equal(ManagedScopeCodec.Encode(original), ManagedScopeCodec.Encode(await ManagedScopeTest.CaptureAsync(test, original)));
        foreach (var mutation in Enum.GetValues<ManagedPolicyDrift>())
        {
            await using var observer = test.Scope();
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(test.Token);
            await ChangeAsync(database, id, mutation, test.Token);
            await transaction.CommitAsync(test.Token);
            var failure = await Record.ExceptionAsync(() => ManagedScopeTest.CaptureAsync(test, original));
            Assert.True(failure is InvalidOperationException, CurrentPolicyBypass);
            await RestoreAsync(database, test.Token);
        }
    }

    /// <summary>Writes one actual native/product field without changing its revision, retaining original values for exact fixture restoration.</summary>
    private static async Task ChangeAsync(PlatformDbContext database, string id, ManagedPolicyDrift mutation, CancellationToken token)
    {
        if (mutation == ManagedPolicyDrift.Permission)
        {
            var row = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(value => value.Id == id, token);
            row.Permissions = row.Permissions!.Replace(OriginalPermission, OtherPermission, StringComparison.Ordinal);
        }
        else if (mutation == ManagedPolicyDrift.Resource)
        {
            var row = await database.Set<OpenIddictEntityFrameworkCoreScope>().SingleAsync(value => value.Name == Orders, token);
            row.Resources = MissingResource;
        }
        else if (mutation is ManagedPolicyDrift.AudienceDisabled or ManagedPolicyDrift.AudienceOwner)
        {
            var row = await database.Set<ApiAudience>().SingleAsync(token);
            if (mutation == ManagedPolicyDrift.AudienceDisabled)
            {
                row.Enabled = false;
            }
            else
            {
                row.Owner += Changed;
            }
        }
        else
        {
            var row = await database.Set<ScopePolicy>().SingleAsync(value => value.Kind == IdentityKind, token);
            if (mutation == ManagedPolicyDrift.ScopeDisabled)
            {
                row.Enabled = false;
            }
            else
            {
                row.ConsentText += Changed;
            }
        }
        await database.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
    }

    /// <summary>Restores the independently retained pre-save original values only after the rejection oracle has observed committed drift.</summary>
    private static async Task RestoreAsync(PlatformDbContext database, CancellationToken token)
    {
        foreach (var entry in database.ChangeTracker.Entries().ToArray())
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Modified;
        }
        await database.SaveChangesAsync(token);
    }
}

/// <summary>Names independently persisted authorities whose drift must invalidate a captured scope decision.</summary>
public enum ManagedPolicyDrift
{
    Permission, Resource, AudienceDisabled, AudienceOwner, ScopeDisabled, Consent
}
