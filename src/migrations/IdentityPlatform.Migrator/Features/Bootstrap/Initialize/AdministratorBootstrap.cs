using System.Security.Claims;
using IdentityPlatform.Core.Features.Identity.Permissions;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

public enum BootstrapOutcome
{
    Created, AlreadyInitialized
}

public sealed class AdministratorBootstrap(PlatformDbContext database, UserManager<PlatformUser> users,
    RoleManager<IdentityRole> roles, TimeProvider time, BootstrapCancellation cancellation)
{
    public async Task<BootstrapOutcome> InitializeAsync(BootstrapInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellation.Token = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = users.NormalizeName(input.UserName);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // A real UPDATE serializes all participating processes before any eligibility read.
        var locked = await database.Set<AdministratorGovernance>().Where(value => value.Id == 1)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.Revision, value => value.Revision + 1), cancellationToken);
        Require(locked == 1);
        var governance = await database.Set<AdministratorGovernance>().SingleAsync(cancellationToken);
        var receipt = await database.Set<AdministratorBootstrapReceipt>().SingleOrDefaultAsync(cancellationToken);
        Require(governance.BootstrapConsumed == (receipt is not null));
        if (receipt is not null)
        {
            Require(receipt.OperationId == input.OperationId && receipt.OriginalNormalizedUserName == normalized &&
                receipt.BundleVersion == AdministratorPermissions.BundleVersion && !string.IsNullOrEmpty(receipt.UserId));
            // Disposing rolls back the lock-only revision update: replay never repairs mutable state.
            return BootstrapOutcome.AlreadyInitialized;
        }
        Require(!await database.Set<AdministratorEnrollment>().AnyAsync(cancellationToken));
        Require(await users.FindByNameAsync(input.UserName) is null);
        // SQL Server equality includes collation and padding rules. Make the final
        // reserved-type decision ordinal, without DISTINCT collapsing variants.
        var candidates = await database.RoleClaims.Where(value => value.ClaimType == AdministratorPermissions.ClaimType)
            .Select(value => value.ClaimType).ToListAsync(cancellationToken);
        Require(!candidates.Contains(AdministratorPermissions.ClaimType, StringComparer.Ordinal));
        foreach (var name in AdministratorPermissions.Bundles.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Require(!await roles.RoleExistsAsync(name));
        }
        foreach (var (name, permissions) in AdministratorPermissions.Bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = new IdentityRole(name);
            Require((await roles.CreateAsync(role)).Succeeded);
            foreach (var permission in permissions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Require((await roles.AddClaimAsync(role, new Claim(AdministratorPermissions.ClaimType, permission))).Succeeded);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var user = new PlatformUser { UserName = input.UserName, IsEnabled = false, LockoutEnabled = true };
        Require((await users.CreateAsync(user, input.Password)).Succeeded);
        cancellationToken.ThrowIfCancellationRequested();
        Require((await users.AddToRoleAsync(user, AdministratorPermissions.PlatformRole)).Succeeded);
        database.Add(new AdministratorEnrollment { UserId = user.Id });
        database.Add(new AdministratorBootstrapReceipt
        {
            OperationId = input.OperationId,
            OriginalNormalizedUserName = normalized,
            UserId = user.Id,
            BundleVersion = AdministratorPermissions.BundleVersion,
            CompletedAt = time.GetUtcNow().ToUnixTimeSeconds()
        });
        governance.BootstrapConsumed = true;
        database.Add(new SecurityAuditIntent
        {
            Event = "administrator-initialization",
            ActorType = "deployment-operator",
            ActorId = input.OperatorReference,
            TargetType = "user",
            TargetId = user.Id,
            Outcome = "enrollment-pending",
            CorrelationId = input.OperationId,
            OccurredAt = time.GetUtcNow().ToUnixTimeSeconds()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BootstrapOutcome.Created;
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Administrator initialization was rejected.");
        }
    }
}
