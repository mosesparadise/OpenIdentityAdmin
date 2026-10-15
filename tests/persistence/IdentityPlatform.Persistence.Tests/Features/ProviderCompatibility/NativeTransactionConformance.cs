using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks real transaction durability and governed update-lock contention with independently classified native failures.</summary>
internal static class NativeTransactionConformance
{
    private const int GovernanceId = 1;
    private const int ContenderCommandSeconds = 15;

    /// <summary>Requires the common lock to block a second transaction, survive commit, and undo a later rollback.</summary>
    internal static async Task VerifyAsync(IServiceProvider services, NativeProviderFixture fixture, CancellationToken token)
    {
        await using var scope = services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await RevisionAsync(owner, token);
        await using (var connection = new CeremonyConnection(owner))
        {
            await connection.OpenAsync(token);
            await using var transaction = await owner.Database.BeginTransactionAsync(token);
            await new CeremonyLocks(owner).AcquireAsync(new CeremonyLockPlan(true, false, [], []), token);
            await RequireContenderDeniedAsync(services, fixture, token);
            await transaction.CommitAsync(token);
        }
        Assert.Equal(before + 1, await RevisionAsync(owner, token));
        await using (var connection = new CeremonyConnection(owner))
        {
            await connection.OpenAsync(token);
            await using var transaction = await owner.Database.BeginTransactionAsync(token);
            await new CeremonyLocks(owner).AcquireAsync(new CeremonyLockPlan(true, false, [], []), token);
            await transaction.RollbackAsync(token);
        }
        Assert.Equal(before + 1, await RevisionAsync(owner, token));
    }

    /// <summary>A separate runtime context must report the fixture's exact lock-timeout failure while the owner holds the row.</summary>
    private static async Task RequireContenderDeniedAsync(IServiceProvider services, NativeProviderFixture fixture, CancellationToken token)
    {
        await using var scope = services.CreateAsyncScope();
        var contender = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var connection = new CeremonyConnection(contender);
        await connection.OpenAsync(token);
        contender.Database.SetCommandTimeout(ContenderCommandSeconds);
        await using var transaction = await contender.Database.BeginTransactionAsync(token);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            new CeremonyLocks(contender).AcquireAsync(new CeremonyLockPlan(true, false, [], []), token));
        var native = exception is DbException direct ? direct :
            Assert.IsAssignableFrom<DbException>(Assert.IsType<InvalidOperationException>(exception).InnerException);
        fixture.RequireLockTimeout(native);
        await transaction.RollbackAsync(token);
    }

    /// <summary>Reads committed authoritative state without EF tracking or implementation-derived expected values.</summary>
    private static Task<long> RevisionAsync(PlatformDbContext database, CancellationToken token) =>
        database.Set<AdministratorGovernance>().AsNoTracking().Where(row => row.Id == GovernanceId)
            .Select(row => row.Revision).SingleAsync(token);
}
