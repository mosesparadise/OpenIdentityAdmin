using System.Data.Common;
using System.Text;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Proves real-provider durability, least privilege, immutable expiry and local denial without activating HTTP consumers.</summary>
public sealed class SessionDurabilityTests
{
    private static readonly int[] PermissionDeniedNumbers = [229, 262, 2760];
    private const string PostgreSql = "PostgreSql";
    private const string DeniedSqlState = "42501";
    private const int IdleMinutes = 5;
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Requires independent-instance state, native precision and audited lifecycle under actual runtime credentials.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ProtectedRuntimeStore_When_RestartedAndRevoked_Then_StateIsDurableAndDenialPersists(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        var handle = await CreateAndInspectAsync(harness, token);
        await using var second = harness.Node();
        var loaded = await second.Store.LoadAsync(handle, token);
        Assert.Equal(SessionTicketStatus.Available, loaded.Status);
        Assert.Equal(BffSessionFixture.TokenSentinel, loaded.Ticket!.Properties.GetTokenValue(BffSessionFixture.TokenName));
        await RenewAndExpireAsync(harness, second.Store, handle, token);
        Assert.Equal(SessionTicketStatus.Available, (await second.Store.RevokeAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Available, (await second.Store.RevokeAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Unavailable, (await second.Store.LoadAsync(handle, token)).Status);
        Assert.Equal(1, (await second.Store.CleanupAsync(token)).Removed);
        Assert.Equal(SessionTicketStatus.Unavailable, (await second.Store.RenewAsync(handle, loaded.Revision, BffSessionFixture.Ticket(), token)).Status);
        await using var database = harness.Database();
        Assert.Empty(await database.Set<AdminSessionTicket>().ToArrayAsync(token));
        Assert.Equal(3, await database.Set<AdminSessionAudit>().CountAsync(token));
        await AssertRuntimeIsolationAsync(harness, provider, token);
        harness.Logs.RequireNoPrivateValues(handle, BffSessionFixture.Subject, BffSessionFixture.SourceSession, BffSessionFixture.TokenSentinel);
    }

    /// <summary>Checks committed metadata and ciphertext before disposing the creating instance.</summary>
    private static async Task<string> CreateAndInspectAsync(SessionStoreHarness harness, CancellationToken token)
    {
        await using var first = harness.Node();
        harness.Clock.Set(BffSessionFixture.Start.AddTicks(1));
        var created = await first.Store.CreateAsync(BffSessionFixture.Ticket(), BffSessionFixture.Subject, BffSessionFixture.SourceSession,
            BffSessionFixture.Start.Add(BffSessionFixture.Absolute), token, BffSessionFixture.Idle);
        Assert.Equal(SessionTicketStatus.Available, created.Status);
        Assert.NotNull(created.Handle);
        await using var database = harness.Database();
        var row = await database.Set<AdminSessionTicket>().SingleAsync(token);
        Assert.Equal(BffSessionFixture.Start.UtcTicks + 1, row.CreatedAt);
        Assert.NotEqual(created.Handle, row.Digest);
        Assert.DoesNotContain(BffSessionFixture.TokenSentinel, Encoding.UTF8.GetString(row.ProtectedTicket));
        Assert.Equal(1, await database.Set<AdminSessionAudit>().CountAsync(token));
        return created.Handle;
    }

    /// <summary>Preserves the five-minute idle policy across restart and renewal while reads, rollback and equality never renew state.</summary>
    private static async Task RenewAndExpireAsync(SessionStoreHarness harness, SessionTicketStore store, string handle, CancellationToken token)
    {
        const int ActivityMinutes = 4;
        const int NewExpiryMinutes = 9;
        harness.Clock.Set(BffSessionFixture.Start.AddMinutes(ActivityMinutes));
        Assert.Equal(SessionTicketStatus.Available, (await store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), token)).Status);
        await using var database = harness.Database();
        var row = await database.Set<AdminSessionTicket>().SingleAsync(token);
        Assert.Equal(BffSessionFixture.Start.AddMinutes(NewExpiryMinutes).UtcTicks, row.IdleExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(IdleMinutes).Ticks, row.IdleLifetime);
        harness.Clock.Set(BffSessionFixture.Start.AddMinutes(ActivityMinutes).AddTicks(-1));
        Assert.Equal(SessionTicketStatus.Unavailable, (await store.LoadAsync(handle, token)).Status);
        harness.Clock.Set(new DateTimeOffset(row.IdleExpiresAt - 1, TimeSpan.Zero));
        Assert.Equal(SessionTicketStatus.Available, (await store.LoadAsync(handle, token)).Status);
        harness.Clock.Set(new DateTimeOffset(row.IdleExpiresAt, TimeSpan.Zero));
        Assert.Equal(SessionTicketStatus.Unavailable, (await store.LoadAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Unavailable, (await store.RenewAsync(handle, row.Revision, BffSessionFixture.Ticket(), token)).Status);
    }

    /// <summary>Requires genuine database authorization failures for runtime DDL and migration-history mutation.</summary>
    private static async Task AssertRuntimeIsolationAsync(SessionStoreHarness harness, string provider, CancellationToken token)
    {
        const string Ddl = "CREATE TABLE bff.MustNeverExist (Id integer)";
        const string PostgreSqlHistory = "DELETE FROM bff.\"SessionMigrations\"";
        const string SqlServerHistory = "DELETE FROM bff.SessionMigrations";
        await using var database = harness.Database(runtime: true);
        var ddl = await Assert.ThrowsAnyAsync<DbException>(() => database.Database.ExecuteSqlRawAsync(Ddl, token));
        var history = await Assert.ThrowsAnyAsync<DbException>(() => database.Database.ExecuteSqlRawAsync(
            provider == PostgreSql ? PostgreSqlHistory : SqlServerHistory, token));
        AssertPermissionDenied(ddl);
        AssertPermissionDenied(history);
    }

    /// <summary>Distinguishes denied privileges from unrelated storage or syntax failures.</summary>
    private static void AssertPermissionDenied(DbException failure)
    {
        if (failure is Npgsql.PostgresException postgres)
        {
            Assert.Equal(DeniedSqlState, postgres.SqlState);
        }
        else
        {
            Assert.Contains(Assert.IsType<Microsoft.Data.SqlClient.SqlException>(failure).Number, PermissionDeniedNumbers);
        }
    }
}
