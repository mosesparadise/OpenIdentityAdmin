using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Exercises provider-native schema, inert backfill and runtime-role constraints.</summary>
[Collection("administrator-bootstrap")]
public sealed class MigrationTests(BootstrapFixture fixture)
{
    private const int DigestLength = 64;
    private const int CreatedAt = 100;
    private const int ExpiresAt = 200;
    private const int ExcessFailures = 6;
    private const int InvalidSingleton = 2;
    private const int FuturePasswordProof = 101;
    private const int PasswordProof = 90;
    private const int FactorProof = 95;

    private const string PostgreSqlProvider = "PostgreSql";
    private const string PostgreSqlPreviousMigration = "20261012110309_AdministratorBootstrap";
    private const string SqlServerPreviousMigration = "20261012110314_AdministratorBootstrap";
    private const string FactorMigrationSuffix = "_AdministratorFactors";
    private const string PendingState = "Pending";
    private const string ProviderTrait = "Provider";
    private const string SqlServerProvider = "SqlServer";
    private const string FactorGenerationViolation = "factor-generation";
    private const string RecoveryGenerationViolation = "recovery-generation";
    private const string ReplayExpiryViolation = "replay-expiry";
    private const string InteractionBudgetViolation = "interaction-budget";
    private const string SessionMethodViolation = "session-method";
    private const string PaddedMethodViolation = "session-padded-method";
    private const string ProofTimeViolation = "session-proof-time";
    private const string AuthenticationBudgetViolation = "authentication-budget";
    private const string AdmissionViolation = "admission-singleton";
    private const string SyntheticMaterial = "synthetic";
    private const string MissingBudget = "absent";
    private const string EnrollmentPurpose = "Enrollment";
    private const string PasswordOnly = "pwd";
    private const string PasswordAndOtp = "pwd otp";
    private const string PaddedPasswordAndOtp = "pwd otp ";

    /// <summary>Upgrades a real previous schema while preserving permanent bootstrap, native credentials and audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PreFactorSchema_When_Upgraded_Then_ExistingBootstrapRemainsInertAndUnchanged(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var token = TestContext.Current.CancellationToken;
        await lease.InitializeAsync(token);
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(token);
        var receipt = await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().SingleAsync(token);
        var previous = provider == PostgreSqlProvider ? PostgreSqlPreviousMigration : SqlServerPreviousMigration;
        await database.GetService<IMigrator>().MigrateAsync(previous, token);
        Assert.DoesNotContain((await database.Database.GetAppliedMigrationsAsync(token)), value => value.EndsWith(FactorMigrationSuffix, StringComparison.Ordinal));
        await database.Database.MigrateAsync(token);
        await database.Database.MigrateAsync(token);
        var current = await database.Users.AsNoTracking().SingleAsync(token);
        Assert.True(current.PasswordHash == user.PasswordHash && current.SecurityStamp == user.SecurityStamp && current.ConcurrencyStamp == user.ConcurrencyStamp);
        Assert.True(!current.IsEnabled && !current.TwoFactorEnabled && current.LockoutEnabled);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token);
        Assert.True(enrollment.State == PendingState && !enrollment.Suspended && enrollment.FactorGeneration == 0 && enrollment.RecoveryGeneration == 0);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(token);
        Assert.True(authentication.UserId == user.Id && authentication.Generation == 1 && authentication.Revision == 0 && authentication.Failures == 0 && authentication.LockoutUntil == null && authentication.ObservedAt == null);
        Assert.Equal(receipt.OperationId, (await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().SingleAsync(token)).OperationId);
        Assert.True((await database.Set<AdministratorGovernance>().AsNoTracking().SingleAsync(token)).BootstrapConsumed);
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(token));
        Assert.Empty(await database.Set<AdministratorFactor>().ToArrayAsync(token));
        Assert.Empty(await database.Set<AdministratorRecoveryCode>().ToArrayAsync(token));
        Assert.Empty(await database.Set<AdministratorSession>().ToArrayAsync(token));
        Assert.Equal(1, (await database.Set<AdministratorAdmission>().SingleAsync(token)).Id);
        Assert.Equal(SchemaCatalog.Platform(DatabaseProviderParser.Parse(provider)), await database.Database.GetAppliedMigrationsAsync(token));
    }

    /// <summary>Rejects padded receipt ownership during backfill even where SQL Server equality ignores trailing spaces.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PaddedReceiptOwner_When_FactorSchemaUpgrades_Then_NoAuthenticationStateIsBackfilled(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var token = TestContext.Current.CancellationToken;
        await lease.InitializeAsync(token);
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var previous = provider == PostgreSqlProvider ? PostgreSqlPreviousMigration : SqlServerPreviousMigration;
        await database.GetService<IMigrator>().MigrateAsync(previous, token);
        const string Padding = " ";
        await database.Set<AdministratorBootstrapReceipt>().ExecuteUpdateAsync(update => update.SetProperty(row => row.UserId, row => row.UserId + Padding), token);
        await database.Database.MigrateAsync(token);
        Assert.Empty(await database.Set<AdministratorAuthentication>().ToArrayAsync(token));
        Assert.False((await database.Users.SingleAsync(token)).IsEnabled);
        Assert.True((await database.Set<AdministratorGovernance>().SingleAsync(token)).BootstrapConsumed);
    }

    /// <summary>Proves the runtime role can persist valid replay/session rows under the same schema used by rejection controls.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ValidBoundedRows_When_RuntimeRolePersists_Then_SessionAndReplayRoundTrip(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        state.Database.Add(new AdministratorSession
        {
            UserId = state.User.Id,
            PublicId = SyntheticMaterial,
            SecurityStamp = state.User.SecurityStamp!,
            CreatedAt = CreatedAt,
            LastActivityAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            PasswordVerifiedAt = PasswordProof,
            FactorVerifiedAt = FactorProof,
            AuthenticationMethods = PasswordAndOtp
        });
        state.Database.Add(new AdministratorReplay
        {
            UserId = state.User.Id,
            AcceptedAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            ProtectedCode = state.Protection.Protect(SyntheticMaterial, state.User.Id, 1, FactorProtection.Replay)
        });
        await state.Database.SaveChangesAsync(state.Token);
        state.Database.ChangeTracker.Clear();
        Assert.Equal(1, await state.Database.Set<AdministratorSession>().CountAsync(state.Token));
        var replay = await state.Database.Set<AdministratorReplay>().SingleAsync(state.Token);
        Assert.True(state.Protection.Unprotect(replay.ProtectedCode, state.User.Id, 1, FactorProtection.Replay) == SyntheticMaterial);
    }

    /// <summary>Enumerates schema constraints without logging database exception payloads or cryptographic material.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> InvalidRows =>
        from provider in Providers
        from invalid in Violations
        select new TheoryDataRow<string, string>(provider, invalid).WithTrait(ProviderTrait, provider);
    private static readonly string[] Providers = [PostgreSqlProvider, SqlServerProvider];
    private static readonly string[] Violations = [FactorGenerationViolation, RecoveryGenerationViolation, ReplayExpiryViolation, InteractionBudgetViolation, SessionMethodViolation, PaddedMethodViolation, ProofTimeViolation, AuthenticationBudgetViolation, AdmissionViolation];

    /// <summary>Proves the actual runtime role cannot persist invalid factor/session state on either provider.</summary>
    [Theory, MemberData(nameof(InvalidRows))]
    public async Task Given_InvalidMaterialState_When_RuntimeRolePersists_Then_DatabaseConstraintsRejectIt(string provider, string invalid)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        AddInvalid(state, invalid);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => state.Database.SaveChangesAsync(state.Token));
        if (provider == PostgreSqlProvider)
        {
            var native = Assert.IsType<Npgsql.PostgresException>(error.InnerException);
            Assert.Equal(invalid == InteractionBudgetViolation ? Npgsql.PostgresErrorCodes.ForeignKeyViolation : Npgsql.PostgresErrorCodes.CheckViolation, native.SqlState);
        }
        else
        {
            Assert.Equal(547, Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException).Number);
        }
        state.Database.ChangeTracker.Clear();
        Assert.Empty(await state.Database.Set<AdministratorSession>().AsNoTracking().ToArrayAsync(state.Token));
        Assert.Empty(await state.Database.Set<AdministratorFactor>().AsNoTracking().ToArrayAsync(state.Token));
    }

    /// <summary>Builds malformed rows through independent fixture expectations, never through production validation helpers.</summary>
    private static void AddInvalid(FactorScenario state, string invalid)
    {
        switch (invalid)
        {
            case FactorGenerationViolation:
                state.Database.Add(new AdministratorFactor { UserId = state.User.Id, Generation = 0, ProtectedSeed = SyntheticMaterial });
                break;
            case RecoveryGenerationViolation:
                state.Database.Add(new AdministratorRecoveryCode { UserId = state.User.Id, Generation = 0, Verifier = new string('A', DigestLength) });
                break;
            case ReplayExpiryViolation:
                state.Database.Add(new AdministratorReplay { UserId = state.User.Id, ProtectedCode = SyntheticMaterial, AcceptedAt = CreatedAt, ExpiresAt = CreatedAt });
                break;
            case InteractionBudgetViolation:
                state.Database.Add(new AdministratorInteraction { UserId = state.User.Id, PublicId = SyntheticMaterial, BrowserBudgetId = MissingBudget, BrowserDigest = SyntheticMaterial, Purpose = EnrollmentPurpose, SecurityStamp = SyntheticMaterial, CreatedAt = CreatedAt, ExpiresAt = ExpiresAt });
                break;
            case AuthenticationBudgetViolation:
                state.Database.Set<AdministratorAuthentication>().Local.Single().Failures = ExcessFailures;
                break;
            case AdmissionViolation:
                state.Database.Add(new AdministratorAdmission { Id = InvalidSingleton });
                break;
            default:
                state.Database.Add(new AdministratorSession
                {
                    UserId = state.User.Id,
                    PublicId = SyntheticMaterial,
                    SecurityStamp = SyntheticMaterial,
                    CreatedAt = CreatedAt,
                    LastActivityAt = CreatedAt,
                    ExpiresAt = ExpiresAt,
                    PasswordVerifiedAt = invalid == ProofTimeViolation ? FuturePasswordProof : PasswordProof,
                    FactorVerifiedAt = FactorProof,
                    AuthenticationMethods = invalid == SessionMethodViolation ? PasswordOnly : invalid == PaddedMethodViolation ? PaddedPasswordAndOtp : PasswordAndOtp
                });
                break;
        }
    }
}
