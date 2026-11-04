using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Contiguous phases of the independent successful bootstrap oracle.</summary>
internal static class BootstrapCreatedEvidence
{
    private const string PendingEnrollment = "Pending";
    private const int ReceiptWindowMinutes = 10;
    private const string MissingNativeStamps = "Native stamps are missing.";
    private const int IdentityPrf = 2;
    private const int IterationOffset = 5;
    private const int PasswordIterations = 210_000;
    private const int SaltLengthOffset = 9;
    private const int MinimumSaltBytes = 16;
    private const string UnexpectedNativeHashPolicy = "Native hash policy differs from the approved contract.";
    private const string UnexpectedPasswordHashRepair = "Password verification repaired the original bootstrap hash.";
    private const string InitializationEvent = "administrator-initialization";
    private const string EnrollmentPendingOutcome = "enrollment-pending";
    private const string DeploymentOperatorType = "deployment-operator";
    private const string SyntheticOperator = "synthetic-operator";
    private const string UserTarget = "user";

    /// <summary>Checks exact inert identity, password, receipt, audit and authority after real initialization.</summary>
    internal static async Task AssertCreatedAsync(BootstrapDatabase lease, string password, Guid operation)
    {
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = (await users.FindByNameAsync(lease.Username))!;
        var receipt = await RequireInitialStateAsync(database, user, lease, operation);
        await RequireNativePasswordAsync(database, users, user, password);
        await RequireAccountIsolationAsync(database);
        await RequireAuditIsolationAsync(database, user, receipt, operation);
        await BootstrapBundles.AssertBundlesAsync(database, user.Id);
    }

    /// <summary>Checks pending enrollment, the immutable receipt, governance revisions and native stamps.</summary>
    private static async Task<AdministratorBootstrapReceipt> RequireInitialStateAsync(PlatformDbContext database, PlatformUser user, BootstrapDatabase lease, Guid operation)
    {
        Assert.False(user.IsEnabled);
        Assert.False(user.TwoFactorEnabled);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.LockoutEnabled);
        Assert.Equal(PendingEnrollment, (await database.Set<AdministratorEnrollment>().SingleAsync(Token)).State);
        Assert.True((await database.Set<AdministratorGovernance>().SingleAsync(Token)).BootstrapConsumed);
        var receipt = await database.Set<AdministratorBootstrapReceipt>().SingleAsync(Token);
        Assert.Equal(operation, receipt.OperationId);
        Assert.Equal(user.Id, receipt.UserId);
        Assert.Equal(lease.Username.ToUpperInvariant(), receipt.OriginalNormalizedUserName);
        Assert.Equal(1, receipt.BundleVersion);
        Assert.InRange(receipt.CompletedAt, DateTimeOffset.UtcNow.AddMinutes(-ReceiptWindowMinutes).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(Token);
        Assert.Equal(user.Id, enrollment.UserId);
        Assert.Equal(0, enrollment.Revision);
        Assert.Equal(1, (await database.Set<AdministratorGovernance>().SingleAsync(Token)).Revision);
        Assert.True(!string.IsNullOrEmpty(user.SecurityStamp) && !string.IsNullOrEmpty(user.ConcurrencyStamp), MissingNativeStamps);
        return receipt;
    }

    /// <summary>Checks the original IdentityV3 envelope and native verification without hash repair.</summary>
    private static async Task RequireNativePasswordAsync(PlatformDbContext database, UserManager<PlatformUser> users, PlatformUser user, string password)
    {
        // IdentityV3 network-byte-order envelope: version, PRF, iteration count, salt length.
        var originalHash = user.PasswordHash!;
        var hash = Convert.FromBase64String(originalHash);
        Assert.True(hash[0] == 1 && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(1)) == IdentityPrf &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(IterationOffset)) == PasswordIterations &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(SaltLengthOffset)) >= MinimumSaltBytes, UnexpectedNativeHashPolicy);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.True(originalHash == (await database.Users.AsNoTracking().SingleAsync(value => value.Id == user.Id, Token)).PasswordHash,
            UnexpectedPasswordHashRepair);
    }

    /// <summary>Requires the sole account to have no claims, external logins or passkeys.</summary>
    private static async Task RequireAccountIsolationAsync(PlatformDbContext database)
    {
        Assert.Single(await database.Users.ToListAsync(Token));
        Assert.Empty(await database.UserClaims.ToListAsync(Token));
        Assert.Empty(await database.UserLogins.ToListAsync(Token));
        Assert.Empty(await database.Set<IdentityUserPasskey<string>>().ToListAsync(Token));
    }

    /// <summary>Checks exact initialization audit attribution and absence of issued session or token authority.</summary>
    private static async Task RequireAuditIsolationAsync(PlatformDbContext database, PlatformUser user, AdministratorBootstrapReceipt receipt, Guid operation)
    {
        var audit = await database.Set<SecurityAuditIntent>().SingleAsync(Token);
        Assert.Equal(InitializationEvent, audit.Event);
        Assert.Equal(EnrollmentPendingOutcome, audit.Outcome);
        Assert.Equal(operation, audit.CorrelationId);
        Assert.Equal(DeploymentOperatorType, audit.ActorType);
        Assert.Equal(SyntheticOperator, audit.ActorId);
        Assert.Equal(UserTarget, audit.TargetType);
        Assert.InRange(audit.OccurredAt, receipt.CompletedAt, receipt.CompletedAt + 1);
        Assert.Equal(user.Id, audit.TargetId);
        Assert.Empty(await database.Set<LocalIdentitySession>().ToListAsync(Token));
        Assert.Empty(await database.Set<IdentityUserToken<string>>().ToListAsync(Token));
        Assert.Empty(await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().ToListAsync(Token));
    }
}
