using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves genuine native password-hash evolution can continue through fresh MFA/consent without accepting arbitrary stamp updates.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedPasswordUpgradeTests(BootstrapFixture fixture)
{
    private const int PreviousIterations = 1000;
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>A real old native hash upgrades once; completed persistence faults roll back and only the exact native successor may bind the new ceremony.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_OlderNativePasswordHash_When_ManagedPasswordUpgradesIt_Then_OnlySuccessfulExactNativeSuccessorCanReachConsent(string provider, ManagedPasswordUpgradeDefect defect)
    {
        var fault = new ManagedPasswordUpgradeFault(defect);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var form = await ManagedHumanHttp.StartAsync(test);
        var original = await InstallOlderHashAsync(test);
        fault.OriginalHash = original.PasswordHash;
        if (defect != ManagedPasswordUpgradeDefect.None)
        {
            var before = await ManagedHumanEvidence.DigestAsync(test);
            using var rejected = await ManagedHumanHttp.PasswordAsync(test, form);
            await ManagedCacheTestContext.UnavailableAsync(rejected);
            Assert.True(fault.Fired && fault.MutationCompleted);
            Assert.Equal(FactorBinding.Digest(original), FactorBinding.Digest(await UserAsync(test)));
            Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
            fault.Disabled = true;
        }
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var upgraded = await UserAsync(test);
        await using var issuer = test.Http.Environment.IssuerServices.CreateAsyncScope();
        Assert.True(upgraded.PasswordHash != original.PasswordHash && upgraded.SecurityStamp != original.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success, issuer.ServiceProvider.GetRequiredService<IPasswordHasher<PlatformUser>>()
            .VerifyHashedPassword(upgraded, upgraded.PasswordHash!, test.Http.Environment.Issuer.Account.Journey.Lease.Password));
        Assert.True(Assert.Single((await test.SnapshotAsync()).Interactions).SecurityStamp == upgraded.SecurityStamp);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        using var factor = await ManagedHumanHttp.MfaAsync(test, mfa);
        await ManagedHumanHttp.EntryAsync(factor);
        var consent = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, factor.Headers.Location!);
        using var decision = await ManagedHumanHttp.DecideAsync(test, consent);
        await ManagedHumanHttp.EntryAsync(decision);
        Assert.Equal(ManagedLoginState.Approved, Assert.Single((await test.SnapshotAsync()).Interactions).State);
    }

    /// <summary>Generates an authentic older native hash, proving the installed native verifier requests an upgrade before persisting it as fixture input.</summary>
    private static async Task<PlatformUser> InstallOlderHashAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await database.Users.SingleAsync(TestContext.Current.CancellationToken);
        var password = test.Http.Environment.Issuer.Account.Journey.Lease.Password;
        await using var issuer = test.Http.Environment.IssuerServices.CreateAsyncScope();
        user.PasswordHash = new PasswordHasher<PlatformUser>(Options.Create(new PasswordHasherOptions { IterationCount = PreviousIterations })).HashPassword(user, password);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, issuer.ServiceProvider.GetRequiredService<IPasswordHasher<PlatformUser>>()
            .VerifyHashedPassword(user, user.PasswordHash, password));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>Reads the complete actual account independently without putting hash/stamp values in diagnostic output.</summary>
    private static async Task<PlatformUser> UserAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Retains explicit provider and independently named fault identities in native discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, ManagedPasswordUpgradeDefect>> Cases => from provider in Providers
                                                                                            from defect in Enum.GetValues<ManagedPasswordUpgradeDefect>()
                                                                                            select new TheoryDataRow<string, ManagedPasswordUpgradeDefect>(provider, defect).WithTrait(ProviderTrait, provider);
}
