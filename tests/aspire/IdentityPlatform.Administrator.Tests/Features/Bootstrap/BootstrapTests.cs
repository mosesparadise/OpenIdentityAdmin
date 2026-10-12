using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

[Collection("administrator-bootstrap")]
public sealed partial class BootstrapTests(BootstrapFixture fixture)
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

    private static async Task AssertCreatedAsync(BootstrapDatabase lease, string password, Guid operation)
    {
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = (await users.FindByNameAsync(lease.Username))!;
        Assert.False(user.IsEnabled);
        Assert.False(user.TwoFactorEnabled);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.LockoutEnabled);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.Equal("Pending", (await database.Set<AdministratorEnrollment>().SingleAsync(Token)).State);
        Assert.True((await database.Set<AdministratorGovernance>().SingleAsync(Token)).BootstrapConsumed);
        var receipt = await database.Set<AdministratorBootstrapReceipt>().SingleAsync(Token);
        Assert.Equal(operation, receipt.OperationId);
        Assert.Equal(user.Id, receipt.UserId);
        Assert.Equal(lease.Username.ToUpperInvariant(), receipt.OriginalNormalizedUserName);
        Assert.Equal(1, receipt.BundleVersion);
        Assert.InRange(receipt.CompletedAt, DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(Token);
        Assert.Equal(user.Id, enrollment.UserId);
        Assert.Equal(0, enrollment.Revision);
        Assert.Equal(1, (await database.Set<AdministratorGovernance>().SingleAsync(Token)).Revision);
        Assert.True(!string.IsNullOrEmpty(user.SecurityStamp) && !string.IsNullOrEmpty(user.ConcurrencyStamp), "Native stamps are missing.");
        // IdentityV3 network-byte-order envelope: version, PRF, iteration count, salt length.
        var hash = Convert.FromBase64String(user.PasswordHash!);
        Assert.True(hash[0] == 1 && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(1)) == 2 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(5)) == 210_000 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(9)) >= 16, "Native hash policy differs from the approved contract.");
        Assert.Single(await database.Users.ToListAsync(Token));
        Assert.Empty(await database.UserClaims.ToListAsync(Token));
        Assert.Empty(await database.UserLogins.ToListAsync(Token));
        Assert.Empty(await database.Set<IdentityUserPasskey<string>>().ToListAsync(Token));
        var audit = await database.Set<SecurityAuditIntent>().SingleAsync(Token);
        Assert.Equal("administrator-initialization", audit.Event);
        Assert.Equal("enrollment-pending", audit.Outcome);
        Assert.Equal(operation, audit.CorrelationId);
        Assert.Equal("deployment-operator", audit.ActorType);
        Assert.Equal("synthetic-operator", audit.ActorId);
        Assert.Equal("user", audit.TargetType);
        Assert.InRange(audit.OccurredAt, receipt.CompletedAt, receipt.CompletedAt + 1);
        Assert.Equal(user.Id, audit.TargetId);
        Assert.Empty(await database.Set<LocalIdentitySession>().ToListAsync(Token));
        Assert.Empty(await database.Set<IdentityUserToken<string>>().ToListAsync(Token));
        Assert.Empty(await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().ToListAsync(Token));
        await AssertBundlesAsync(database, user.Id);
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

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Require(IdentityResult result) => Assert.True(result.Succeeded, "Synthetic manager preparation failed.");

    private static async Task<(int ExitCode, string Output)> RunAsync(BootstrapDatabase lease, string provider, byte[] input)
    {
        await using var process = new BootstrapProcess(provider, lease.RuntimeConnection);
        await process.SendAsync(input);
        var result = await process.CompleteAsync();
        using var document = JsonDocument.Parse(input);
        var suppliedPassword = document.RootElement.GetProperty("password").GetString()!;
        Assert.False(result.Output.Contains(suppliedPassword, StringComparison.Ordinal), "Bootstrap output disclosed a credential.");
        return result;
    }

    private static async Task<string> SnapshotAsync(BootstrapDatabase lease, bool includeBootstrap = true)
    {
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var state = new List<object>
        {
            await database.Users.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Roles.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserRoles.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.RoleId).ToListAsync(Token),
            await database.RoleClaims.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserClaims.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserLogins.AsNoTracking().OrderBy(value => value.LoginProvider).ThenBy(value => value.ProviderKey).ToListAsync(Token),
            await database.UserTokens.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.LoginProvider).ThenBy(value => value.Name).ToListAsync(Token),
            await database.Set<IdentityUserPasskey<string>>().AsNoTracking().OrderBy(value => value.CredentialId).ToListAsync(Token),
            await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<LoginBrowserBudget>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<PilotManifest>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token)
        };
        if (includeBootstrap)
        {
            state.AddRange([
            await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(value => value.UserId).ToListAsync(Token),
            await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<AdministratorGovernance>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token)
            ]);
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }

    private static async Task AssertBundlesAsync(PlatformDbContext database, string userId)
    {
        // Independent approved contract values: do not derive the oracle from production constants.
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplicationAdministrator"] = "applications.read applications.write applications.credentials applications.disable scopes.read scopes.write audiences.read audiences.write configuration.read configuration.evaluate",
            ["IdentityAdministrator"] = "users.read users.write users.disable users.unlock users.recovery roles.read roles.write roles.permissions grants.read grants.revoke sessions.read sessions.revoke",
            ["Auditor"] = "applications.read scopes.read audiences.read users.read roles.read grants.read sessions.read audit.read configuration.read keys.read"
        };
        expected["PlatformAdministrator"] = string.Join(' ', expected.Values.SelectMany(value => value.Split(' ')).Append("administrators.assign").Distinct(StringComparer.Ordinal));
        var roles = await database.Roles.ToListAsync(Token);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), roles.Select(value => value.Name!).Order(StringComparer.Ordinal));
        foreach (var role in roles)
        {
            var claims = await database.RoleClaims.Where(value => value.RoleId == role.Id).ToListAsync(Token);
            Assert.All(claims, claim => Assert.Equal("identityplatform.permission", claim.ClaimType));
            Assert.Equal(expected[role.Name!].Split(' ').Order(StringComparer.Ordinal), claims.Select(value => value.ClaimValue!).Order(StringComparer.Ordinal));
        }
        var binding = await database.UserRoles.SingleAsync(value => value.UserId == userId, Token);
        Assert.Equal(roles.Single(value => value.Name == "PlatformAdministrator").Id, binding.RoleId);
    }
}
