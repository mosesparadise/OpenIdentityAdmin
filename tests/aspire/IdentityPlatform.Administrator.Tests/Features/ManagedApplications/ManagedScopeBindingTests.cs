using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Requires current public human admission to reject malformed durable policy even when its digest was changed to match.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopeBindingTests(BootstrapFixture fixture)
{
    private const string Bypass = "managed-policy-binding-bypass";
    private const string Whitespace = " ";

    /// <summary>A substituted stored binding cannot consume password proof or publish a next-phase form; the native policy guard has a causal mutation oracle.</summary>
    [Theory]
    [Trait("CriticalDecision", "managed-policy-binding")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineManagedForm_When_PersistedPolicyIsSubstituted_Then_PasswordProofCannotAdvance(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await ManagedHumanHttp.StartAsync(test);
        await using (var observer = test.Http.Scope())
        {
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<ManagedLoginInteraction>().SingleAsync(TestContext.Current.CancellationToken);
            row.PolicySnapshot += Whitespace;
            row.PolicyDigest = ManagedLoginRules.Digest(row.PolicySnapshot);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var before = await ManagedHumanEvidence.DigestAsync(test);
        using var response = await ManagedHumanHttp.PasswordAsync(test, form);
        var actual = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.True(actual.State == ManagedLoginState.PendingPassword && actual.PasswordVerifiedAt is null, Bypass);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }
}
