using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapCreatedEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapSnapshot;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

[Collection("administrator-bootstrap")]
public sealed class BootstrapTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Creation = ["process", "minimum", "maximum"];
    private static readonly string[] Replay = ["ordinary", "password", "disabled", "deleted", "membership", "renamed"];
    private static readonly string[] Conflicts = ["normalized-user", "reserved-role", "pending", "consumed-without-receipt", "receipt-without-consumed", "missing-singleton", "different-operation", "different-identity", "unowned-permission"];
    public static IEnumerable<TheoryDataRow<string, string>> CreationCases => Cases(Creation);
    public static IEnumerable<TheoryDataRow<string, string>> ReplayCases => Cases(Replay);
    public static IEnumerable<TheoryDataRow<string, string>> ConflictCases => Cases(Conflicts);
    private static IEnumerable<TheoryDataRow<string, string>> Cases(IEnumerable<string> cases) =>
        from provider in Providers from scenario in cases select new TheoryDataRow<string, string>(provider, scenario).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(CreationCases))]
    public async Task Creates_exact_inert_identity_with_native_password_and_atomic_audit(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var password = scenario == "minimum" ? new string('a', 16) : scenario == "maximum" ? new string('a', 256) : lease.Password;
        var result = await RunAsync(lease, provider, lease.Input(password: password));
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Output.Contains("Administrator enrollment initialized.", StringComparison.Ordinal), "Creation outcome missing.");
        await AssertCreatedAsync(lease, password, lease.OperationId);
    }

    [Theory]
    [MemberData(nameof(ReplayCases))]
    public async Task Matching_receipt_never_repairs_changed_account_or_authority(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        Assert.Equal(BootstrapOutcome.Created, await lease.InitializeAsync(Token));
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(lease.Username))!;
            switch (scenario)
            {
                case "password":
                    Require(await users.RemovePasswordAsync(user));
                    Require(await users.AddPasswordAsync(user, new string('b', 20)));
                    break;
                case "disabled":
                    user.AccessFailedCount = 3;
                    user.IsEnabled = false;
                    Require(await users.UpdateAsync(user));
                    break;
                case "deleted":
                    Require(await users.DeleteAsync(user));
                    break;
                case "membership":
                    Require(await users.RemoveFromRoleAsync(user, "PlatformAdministrator"));
                    break;
                case "renamed":
                    Require(await users.SetUserNameAsync(user, "renamed.administrator"));
                    break;
            }
        }
        var before = await SnapshotAsync(lease);
        var result = await RunAsync(lease, provider, lease.Input(password: new string('z', 20)));
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Output.Contains("already initialized", StringComparison.Ordinal), "Reconciliation outcome missing.");
        Assert.True(before == await SnapshotAsync(lease), "Replay changed account, authority, receipt, revision or audit.");
    }

    [Theory]
    [MemberData(nameof(ConflictCases))]
    public async Task Rejects_conflicts_without_adoption_repair_or_partial_mutation(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        if (scenario is "different-operation" or "different-identity")
        {
            await lease.InitializeAsync(Token);
        }
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            switch (scenario)
            {
                case "normalized-user":
                    Require(await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().CreateAsync(new PlatformUser { UserName = lease.Username.ToUpperInvariant() }, lease.Password));
                    break;
                case "reserved-role":
                    Require(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole("platformadministrator")));
                    break;
                case "unowned-permission":
                    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    var role = new IdentityRole("custom-role");
                    Require(await roles.CreateAsync(role));
                    Require(await roles.AddClaimAsync(role, new System.Security.Claims.Claim("identityplatform.permission", "administrators.assign")));
                    break;
                case "pending":
                    database.Add(new AdministratorEnrollment { UserId = "unowned" });
                    break;
                case "consumed-without-receipt":
                    (await database.Set<AdministratorGovernance>().SingleAsync(Token)).BootstrapConsumed = true;
                    break;
                case "receipt-without-consumed":
                    database.Add(new AdministratorBootstrapReceipt { OperationId = lease.OperationId, OriginalNormalizedUserName = lease.Username.ToUpperInvariant(), UserId = "unowned", BundleVersion = 1 });
                    break;
                case "missing-singleton":
                    database.Remove(await database.Set<AdministratorGovernance>().SingleAsync(Token));
                    break;
            }
            await database.SaveChangesAsync(Token);
        }
        var before = await SnapshotAsync(lease);
        var result = await RunAsync(lease, provider, lease.Input(operation: scenario == "different-identity" ? lease.OperationId : Guid.NewGuid(), username: scenario == "different-identity" ? "different.admin" : null));
        Assert.Equal(1, result.ExitCode);
        Assert.True(before == await SnapshotAsync(lease), "Rejected bootstrap changed persisted state.");
    }

    private static readonly string[] ClaimVariants = ["case", "padding", "case-and-exact", "padding-and-exact"];
    public static IEnumerable<TheoryDataRow<string, string>> ClaimCases => Cases(ClaimVariants);

    [Theory]
    [MemberData(nameof(ClaimCases))]
    public async Task Reserved_permission_type_uses_ordinal_semantics_on_both_providers(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = new IdentityRole("unrelated-role");
            Require(await roles.CreateAsync(role));
            var variant = scenario.StartsWith("case", StringComparison.Ordinal) ? "IDENTITYPLATFORM.PERMISSION" : "identityplatform.permission ";
            Require(await roles.AddClaimAsync(role, new System.Security.Claims.Claim(variant, "administrators.assign")));
            if (scenario.EndsWith("-exact", StringComparison.Ordinal))
            {
                Require(await roles.AddClaimAsync(role, new System.Security.Claims.Claim("identityplatform.permission", "administrators.assign")));
            }
        }
        var before = await SnapshotAsync(lease);
        var result = await RunAsync(lease, provider, lease.Input());
        var conflict = scenario.EndsWith("-exact", StringComparison.Ordinal);
        Assert.Equal(conflict ? 1 : 0, result.ExitCode);
        if (conflict)
        {
            Assert.True(before == await SnapshotAsync(lease), "Exact reserved claim collision changed state.");
        }
    }




}
