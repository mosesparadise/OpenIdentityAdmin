using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Separates canonical/current policy validity from proof that the user actually saw and approved that exact policy.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedConsentBindingTests(BootstrapFixture fixture)
{
    private const string Requested = "email openid";
    private const string IdentityKind = "identity";
    private const string Changed = "Different current consent";
    private const string Bypass = "managed-policy-binding-bypass";
    private static readonly string[] Names = ["email", "openid"];

    /// <summary>A valid new stored decision cannot replace the exact policy digest protected in an earlier displayed consent form.</summary>
    [Theory]
    [Trait("CriticalDecision", "managed-policy-binding")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RenderedConsent_When_CurrentDecisionIsReboundWithoutNewHumanApproval_Then_OriginalFormCannotApprove(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        var flow = new ManagedTokenFlow(test, Requested);
        await flow.CacheAsync();
        var form = await flow.ConsentAsync();
        await RebindAsync(test);
        var before = await ManagedHumanEvidence.DigestAsync(test);
        using var response = await ManagedHumanHttp.DecideAsync(test, form);
        var actual = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.True(actual.State == ManagedLoginState.PendingConsent && actual.ConsentVerifiedAt is null, Bypass);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }

    /// <summary>Creates independently current same-membership metadata and canonical policy, deliberately leaving the earlier protected form untouched.</summary>
    private static async Task RebindAsync(ManagedCacheTestContext test)
    {
        await using (var observer = test.Http.Scope())
        {
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var policy = await database.Set<ScopePolicy>().SingleAsync(row => row.Kind == IdentityKind, TestContext.Current.CancellationToken);
            policy.ConsentText = Changed;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var request = new ProtocolStorageRequest(test.Http.Environment.IssuerServices.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken);
        try
        {
            await request.RunAsync(test.Http.Environment.IssuerServices.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                var decision = await ManagedScopePolicy.CaptureAsync(scope, test.ClientIdentifier, Names, Names, token);
                await scope.Owner.RunAsync(async current =>
                {
                    var row = await scope.Owner.Database.Set<ManagedLoginInteraction>().SingleAsync(current);
                    row.PolicySnapshot = ManagedScopeCodec.Encode(decision);
                    row.PolicyDigest = ManagedLoginRules.Digest(row.PolicySnapshot);
                    await scope.Owner.Database.SaveChangesAsync(current);
                    await ManagedScopeReferences.VerifyBoundAsync(scope.Owner.Database, row, current);
                }, token, write: true);
                await transaction.CommitAsync(token);
                return true;
            });
        }
        finally { await request.DrainAsync(); }
    }
}
