using System.Globalization;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("bootstrap")]
public sealed partial class BootstrapAcceptanceTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] ExistingStates = [false, true];
    private static readonly string[] Conflicts =
    [
        .. Enumerable.Range(1, 7).Select(value => "unowned-" + value.ToString(CultureInfo.InvariantCulture)),
        .. Enumerable.Range(1, 7).Select(value => "missing-" + value.ToString(CultureInfo.InvariantCulture)),
        "manifest-user", "manifest-application", "manifest-scope", "manifest-username", "manifest-callback",
        "user-rename", "user-disabled", "user-locked", "user-lockout-disabled", "user-mfa", "user-role",
        "user-password-missing", "user-password-changed",
        "client-redirect", "client-extra-permission", "client-missing-permission", "client-extra-requirement",
        "client-missing-requirement", "client-type", "client-application-type", "client-consent-type", "client-setting",
        "client-disabled", "client-profile", "client-secret",
        "scope-disabled", "scope-profile", "scope-missing-resource", "scope-extra-resource", "scope-wrong-resource"
    ];

    public static IEnumerable<TheoryDataRow<string, string>> ConflictCases =>
        from provider in Providers
        from conflict in Conflicts
        select new TheoryDataRow<string, string>(provider, conflict).WithTrait("Provider", provider);

    public static IEnumerable<TheoryDataRow<string, bool>> AuditCases =>
        from provider in Providers
        from existing in ExistingStates
        select new TheoryDataRow<string, bool>(provider, existing).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(ConflictCases))]
    public async Task Persisted_conflicts_are_rejected_without_repair_or_partial_writes(string provider, string conflict)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var cancellationToken = TestContext.Current.CancellationToken;
        await AssertEmptyAsync(lease, cancellationToken);
        await lease.InitializeAsync(cancellationToken);
        await CorruptAsync(lease, conflict, cancellationToken);
        var before = await SnapshotAsync(lease, cancellationToken);
        var failure = await Record.ExceptionAsync(() => lease.InitializeAsync(cancellationToken));
        Assert.True(failure is PilotConfigurationException, "Persisted conflict must cause a sanitized configuration rejection.");
        Assert.True(before == await SnapshotAsync(lease, cancellationToken), "Rejected initialization changed persisted state.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Valid_no_op_preserves_credentials_and_nonzero_password_failure_count(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var cancellationToken = TestContext.Current.CancellationToken;
        await AssertEmptyAsync(lease, cancellationToken);
        await lease.InitializeAsync(cancellationToken);
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(lease.Username))!;
            Require(await users.AccessFailedAsync(user));
            Require(await users.AccessFailedAsync(user));
            Assert.Equal(2, user.AccessFailedCount);
        }
        var before = await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true);
        await lease.InitializeAsync(cancellationToken);
        Assert.True(before == await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true), "No-op bootstrap altered owned state or credentials.");
        await AssertCoherentAsync(lease, created: 1, noOps: 1, cancellationToken);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Simultaneous_first_initialization_has_one_complete_winner_and_can_be_explicitly_retried(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = deadline.Token;
        await AssertEmptyAsync(lease, cancellationToken);
        var rendezvous = new BootstrapCreationBarrier(cancellationToken);
        await using var competing = lease.WithInterceptor(rendezvous);
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => lease.InitializeAsync(cancellationToken, competing)).AsTask(),
            Record.ExceptionAsync(() => lease.InitializeAsync(cancellationToken, competing)).AsTask());
        Assert.True(rendezvous.Arrivals == 2, "Both independent contexts must select first creation before either insert.");
        Assert.True(results.Count(value => value is null) == 1, "Exactly one first initialization must succeed.");
        var loser = results.Single(value => value is not null);
        Assert.True(IsUniqueConflict(loser), "The losing initialization must fail with the expected native uniqueness conflict.");
        await AssertCoherentAsync(lease, created: 1, noOps: 0, cancellationToken);
        var before = await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true);
        await lease.InitializeAsync(cancellationToken);
        Assert.True(before == await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true), "Explicit retry changed winning state.");
        await AssertCoherentAsync(lease, created: 1, noOps: 1, cancellationToken);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Single_caller_rendezvous_can_be_cancelled_without_a_stranded_transaction(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var rendezvous = new BootstrapCreationBarrier(deadline.Token);
        await using var competing = lease.WithInterceptor(rendezvous);
        var attempt = Record.ExceptionAsync(() => lease.InitializeAsync(deadline.Token, competing)).AsTask();
        try
        {
            await rendezvous.FirstArrival.WaitAsync(deadline.Token);
            Assert.Equal(1, rendezvous.Arrivals);
        }
        finally
        {
            await deadline.CancelAsync();
            // Drain the real operation before checking state or dropping its database.
            await attempt;
        }
        Assert.True(await attempt is OperationCanceledException, "A single-arrival rendezvous must cancel cooperatively.");
        await AssertEmptyAsync(lease, TestContext.Current.CancellationToken);
        await lease.InitializeAsync(TestContext.Current.CancellationToken);
        await AssertCoherentAsync(lease, created: 1, noOps: 0, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(AuditCases))]
    public async Task Audit_permission_failure_rolls_back_creation_and_verification_then_allows_explicit_retry(string provider, bool existing)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var cancellationToken = TestContext.Current.CancellationToken;
        await AssertEmptyAsync(lease, cancellationToken);
        if (existing)
        {
            await lease.InitializeAsync(cancellationToken);
        }
        var before = await SnapshotAsync(lease, cancellationToken);
        try
        {
            await lease.AuditPermissionAsync(allow: false, cancellationToken);
            var failure = await Record.ExceptionAsync(() => lease.InitializeAsync(cancellationToken));
            Assert.True(IsPermissionDenied(failure), "Expected a real database INSERT permission denial.");
            Assert.True(before == await SnapshotAsync(lease, cancellationToken), "Audit failure left partial bootstrap state.");
            if (!existing)
            {
                await AssertEmptyAsync(lease, cancellationToken);
            }
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var failure = await Record.ExceptionAsync(() => lease.AuditPermissionAsync(allow: true, restoration.Token));
            Assert.True(failure is null, "Bootstrap audit permission restoration failed.");
        }
        await lease.InitializeAsync(cancellationToken);
        await AssertCoherentAsync(lease, created: 1, noOps: existing ? 1 : 0, cancellationToken);
    }

    private static bool IsPermissionDenied(Exception? failure) => failure is DbUpdateException && failure.InnerException is
        PostgresException { SqlState: PostgresErrorCodes.InsufficientPrivilege } or SqlException { Number: 229 };

    private static bool IsUniqueConflict(Exception? failure) => failure is DbUpdateException && failure.InnerException is
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } or SqlException { Number: 2601 or 2627 };

    private static void Require(IdentityResult result) => Assert.True(result.Succeeded, "Unable to seed the intended account state.");

    private static async Task CorruptAsync(BootstrapDatabase lease, string conflict, CancellationToken cancellationToken)
    {
        await using var scope = lease.Administrator.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = services.GetRequiredService<IOpenIddictScopeManager>();
        var user = (await users.FindByNameAsync(lease.Username))!;
        var application = (await applications.FindByClientIdAsync(PilotProfile.ClientId, cancellationToken))!;
        var resource = (await scopes.FindByNameAsync(PilotProfile.Scope, cancellationToken))!;
        var manifest = await database.Set<PilotManifest>().SingleAsync(cancellationToken);
        if (conflict.StartsWith("unowned-", StringComparison.Ordinal) || conflict.StartsWith("missing-", StringComparison.Ordinal))
        {
            var unowned = conflict.StartsWith("unowned-", StringComparison.Ordinal);
            var mask = int.Parse(conflict.AsSpan(conflict.LastIndexOf('-') + 1), CultureInfo.InvariantCulture);
            var removed = unowned ? 7 ^ mask : mask;
            if (unowned)
            {
                database.Remove(manifest);
                await database.SaveChangesAsync(cancellationToken);
            }
            if ((removed & 1) != 0)
            {
                Require(await users.DeleteAsync(user));
            }
            if ((removed & 2) != 0)
            {
                await applications.DeleteAsync(application, cancellationToken);
            }
            if ((removed & 4) != 0)
            {
                await scopes.DeleteAsync(resource, cancellationToken);
            }
            return;
        }
        if (conflict.StartsWith("manifest-", StringComparison.Ordinal))
        {
            switch (conflict)
            {
                case "manifest-user":
                    manifest.UserId = "unowned-user";
                    break;
                case "manifest-application":
                    manifest.ApplicationId = "unowned-application";
                    break;
                case "manifest-scope":
                    manifest.ScopeId = "unowned-scope";
                    break;
                case "manifest-username":
                    manifest.UserName = "changed.synthetic";
                    break;
                case "manifest-callback":
                    manifest.RedirectUri = "https://localhost/changed";
                    break;
            }
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        if (conflict.StartsWith("user-", StringComparison.Ordinal))
        {
            switch (conflict)
            {
                case "user-rename":
                    Require(await users.SetUserNameAsync(user, "changed.synthetic"));
                    return;
                case "user-disabled":
                    user.IsEnabled = false;
                    break;
                case "user-locked":
                    Require(await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1)));
                    return;
                case "user-lockout-disabled":
                    Require(await users.SetLockoutEnabledAsync(user, false));
                    return;
                case "user-mfa":
                    Require(await users.SetTwoFactorEnabledAsync(user, true));
                    return;
                case "user-role":
                    Require(await services.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new("synthetic-role")));
                    Require(await users.AddToRoleAsync(user, "synthetic-role"));
                    return;
                case "user-password-missing":
                    Require(await users.RemovePasswordAsync(user));
                    return;
                case "user-password-changed":
                    Require(await users.ChangePasswordAsync(user, lease.Password, "aA1" + Guid.NewGuid().ToString("N")));
                    return;
            }
            Require(await users.UpdateAsync(user));
            return;
        }
        if (conflict.StartsWith("client-", StringComparison.Ordinal))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, application, cancellationToken);
            switch (conflict)
            {
                case "client-redirect":
                    descriptor.RedirectUris.Clear();
                    descriptor.RedirectUris.Add(new("https://localhost/changed"));
                    break;
                case "client-extra-permission":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
                    break;
                case "client-missing-permission":
                    descriptor.Permissions.Remove(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;
                case "client-extra-requirement":
                    descriptor.Requirements.Add("synthetic-extra-requirement");
                    break;
                case "client-missing-requirement":
                    descriptor.Requirements.Clear();
                    break;
                case "client-type":
                    descriptor.ClientType = OpenIddictConstants.ClientTypes.Public;
                    descriptor.ClientSecret = null;
                    break;
                case "client-application-type":
                    descriptor.ApplicationType = OpenIddictConstants.ApplicationTypes.Native;
                    break;
                case "client-consent-type":
                    descriptor.ConsentType = OpenIddictConstants.ConsentTypes.Explicit;
                    break;
                case "client-setting":
                    descriptor.Settings[OpenIddictConstants.Settings.TokenLifetimes.AuthorizationCode] = "00:00:30";
                    break;
                case "client-disabled":
                    descriptor.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                    break;
                case "client-profile":
                    descriptor.Properties["profile"] = JsonSerializer.SerializeToElement("unowned-profile");
                    break;
                case "client-secret":
                    descriptor.ClientSecret = Guid.NewGuid().ToString("N");
                    break;
            }
            await applications.UpdateAsync(application, descriptor, cancellationToken);
            return;
        }
        var scopeDescriptor = new OpenIddictScopeDescriptor();
        await scopes.PopulateAsync(scopeDescriptor, resource, cancellationToken);
        switch (conflict)
        {
            case "scope-disabled":
                scopeDescriptor.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                break;
            case "scope-profile":
                scopeDescriptor.Properties["profile"] = JsonSerializer.SerializeToElement("unowned-profile");
                break;
            case "scope-missing-resource":
                scopeDescriptor.Resources.Clear();
                break;
            case "scope-extra-resource":
                scopeDescriptor.Resources.Add("extra-resource");
                break;
            case "scope-wrong-resource":
                scopeDescriptor.Resources.Clear();
                scopeDescriptor.Resources.Add("changed-resource");
                break;
            default:
                throw new InvalidOperationException("Unknown bootstrap conflict case.");
        }
        await scopes.UpdateAsync(resource, scopeDescriptor, cancellationToken);
    }

    private static async Task AssertEmptyAsync(BootstrapDatabase lease, CancellationToken cancellationToken)
    {
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await AssertNoRelatedStateAsync(database, cancellationToken);
        Assert.Equal(0, await database.Users.CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<OpenIddictEntityFrameworkCoreScope>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<PilotManifest>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<SecurityAuditIntent>().CountAsync(cancellationToken));
    }

    private static async Task AssertCoherentAsync(BootstrapDatabase lease, int created, int noOps, CancellationToken cancellationToken)
    {
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await AssertNoRelatedStateAsync(database, cancellationToken);
        var user = await database.Users.SingleAsync(cancellationToken);
        var application = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(cancellationToken);
        var resource = await database.Set<OpenIddictEntityFrameworkCoreScope>().SingleAsync(cancellationToken);
        var manifest = await database.Set<PilotManifest>().SingleAsync(cancellationToken);
        Assert.True(manifest.UserId == user.Id && manifest.ApplicationId == application.Id && manifest.ScopeId == resource.Id,
            "Bootstrap ownership must reference the single winning native record set.");
        Assert.True(manifest.UserName == lease.Username && manifest.RedirectUri == lease.Callback, "Bootstrap manifest identity drifted.");
        var audits = await database.Set<SecurityAuditIntent>().ToArrayAsync(cancellationToken);
        Assert.True(audits.Length == created + noOps && audits.All(value => value.Event == "pilot-initialization" &&
            value.ActorType == "local-operator" && value.ActorId == null && value.TargetId == user.Id && value.TargetType == "pilot-account" &&
            value.CorrelationId != Guid.Empty && value.OccurredAt > 0), "Bootstrap audit attribution is incomplete.");
        Assert.Equal(created, audits.Count(value => value.Outcome == "created"));
        Assert.Equal(noOps, audits.Count(value => value.Outcome == "verified-no-op"));
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().CheckPasswordAsync(user, lease.Password),
            "Winning initialization did not preserve the supplied credential.");
        Assert.True(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().ValidateClientSecretAsync(application, lease.ClientSecret, cancellationToken),
            "Winning initialization did not preserve the supplied client credential.");
    }

    private static async Task AssertNoRelatedStateAsync(PlatformDbContext database, CancellationToken cancellationToken)
    {
        Assert.Equal(0, await database.Roles.CountAsync(cancellationToken));
        Assert.Equal(0, await database.UserRoles.CountAsync(cancellationToken));
        Assert.Equal(0, await database.UserClaims.CountAsync(cancellationToken));
        Assert.Equal(0, await database.RoleClaims.CountAsync(cancellationToken));
        Assert.Equal(0, await database.UserLogins.CountAsync(cancellationToken));
        Assert.Equal(0, await database.UserTokens.CountAsync(cancellationToken));
        Assert.Equal(0, await database.UserPasskeys.CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<LocalIdentitySession>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<LoginInteraction>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(cancellationToken));
        Assert.Equal(0, await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync(cancellationToken));
    }

    // Complete scalar snapshots stay in process only; failure diagnostics never receive these values.
    private static async Task<string> SnapshotAsync(BootstrapDatabase lease, CancellationToken cancellationToken, bool createdAuditOnly = false)
    {
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return JsonSerializer.Serialize(new
        {
            Users = await database.Users.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Roles = await database.Roles.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Memberships = await database.UserRoles.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.RoleId).ToArrayAsync(cancellationToken),
            UserClaims = await database.UserClaims.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            RoleClaims = await database.RoleClaims.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Logins = await database.UserLogins.AsNoTracking().OrderBy(value => value.LoginProvider).ThenBy(value => value.ProviderKey).ToArrayAsync(cancellationToken),
            UserTokens = await database.UserTokens.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.LoginProvider).ThenBy(value => value.Name).ToArrayAsync(cancellationToken),
            Passkeys = await database.UserPasskeys.AsNoTracking().OrderBy(value => value.CredentialId).ToArrayAsync(cancellationToken),
            Applications = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Scopes = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Manifests = await database.Set<PilotManifest>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(value => !createdAuditOnly || value.Outcome == "created")
                .OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Sessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Interactions = await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken),
            Authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken)
        });
    }
}
