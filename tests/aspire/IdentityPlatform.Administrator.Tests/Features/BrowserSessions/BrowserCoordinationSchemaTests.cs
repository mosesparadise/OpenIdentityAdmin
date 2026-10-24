using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises migrated constraints and inert-seed ownership through real Sessions runtime credentials.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationSchemaTests(BootstrapFixture fixture)
{
    private const string PostgresCheck = "23514";
    private const string PostgresReference = "23503";
    private const int SqlConstraint = 547;

    /// <summary>Nullable native references coexist, invalid child ownership and lifetimes fail natively, and a missing quota seed is never repaired.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_MigratedBrowserSchema_When_InvalidRowsOrMissingSeedAreUsed_Then_NativeConstraintsAndOwnershipDeny(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var first = BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks);
        var second = BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks);
        await scenario.RunAsync(async (database, _) =>
        {
            database.AddRange(first, second, BrowserCoordinationSeeds.Intent(first), BrowserCoordinationSeeds.Intent(second));
            return await database.SaveChangesAsync(BrowserCoordinationFixture.Token);
        });
        var invalid = BrowserCoordinationSeeds.Intent(first);
        invalid.ExpiresAt = invalid.CreatedAt;
        await RequireConstraintAsync(scenario, invalid);
        invalid = BrowserCoordinationSeeds.Intent(BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks));
        await RequireConstraintAsync(scenario, invalid);
        await scenario.RunAsync((database, _) => database.Set<BrowserSessionBudget>().ExecuteDeleteAsync(BrowserCoordinationFixture.Token));
        var before = await scenario.SnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.StartAsync(BrowserCoordinationFixture.Digest(), BrowserCoordinationFixture.Digest()));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Requires the actual provider check/reference failure and independently proves complete rollback.</summary>
    private static async Task RequireConstraintAsync(BrowserCoordinationFixture scenario, BrowserLoginIntent row)
    {
        var before = await scenario.SnapshotAsync();
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.RunAsync(async (database, _) =>
        {
            database.Add(row);
            return await database.SaveChangesAsync(BrowserCoordinationFixture.Token);
        }));
        Assert.True(error.InnerException is Npgsql.PostgresException { SqlState: PostgresCheck or PostgresReference } or
            Microsoft.Data.SqlClient.SqlException { Number: SqlConstraint });
        Assert.True(before == await scenario.SnapshotAsync());
    }
}
