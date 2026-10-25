using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Privately retains exactly three genuine B3 rows to demonstrate selective restoration consequences, not database backup restoration.</summary>
internal sealed class BrowserRestoreSnapshot(PropertyValues context, PropertyValues intent, PropertyValues ticket)
{
    private const string Payload = nameof(AdminSessionTicket.ProtectedTicket);

    /// <summary>Captures all original scalar/blob fields after real callback authentication and requires matching current ticket provenance.</summary>
    internal static Task<BrowserRestoreSnapshot> CaptureAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        var browser = await database.Set<BrowserContext>().SingleAsync(TestContext.Current.CancellationToken);
        var login = await database.Set<BrowserLoginIntent>().SingleAsync(TestContext.Current.CancellationToken);
        var row = await database.Set<AdminSessionTicket>().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BrowserIntentState.Established, login.State);
        Assert.Equal(row.Digest, browser.TicketDigest);
        Assert.Equal(row.Digest, login.TicketDigest);
        Assert.Null(row.RevokedAt);
        var saved = database.Entry(row).CurrentValues.Clone();
        saved[Payload] = row.ProtectedTicket.ToArray();
        return new BrowserRestoreSnapshot(database.Entry(browser).CurrentValues.Clone(), database.Entry(login).CurrentValues.Clone(), saved);
    });

    /// <summary>Restores only the captured rows under browser writer ordering after proving a genuine logout; audit history is deliberately retained.</summary>
    internal Task<bool> RestoreAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, scenario.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken);
        var browser = await database.Set<BrowserContext>().SingleAsync(owner.Token);
        var login = await database.Set<BrowserLoginIntent>().SingleAsync(owner.Token);
        var row = await database.Set<AdminSessionTicket>().SingleAsync(owner.Token);
        Assert.Equal(context.GetValue<long>(nameof(BrowserContext.Generation)) + 1, browser.Generation);
        Assert.Null(browser.TicketDigest);
        Assert.NotNull(row.RevokedAt);
        database.Entry(browser).CurrentValues.SetValues(context);
        database.Entry(login).CurrentValues.SetValues(intent);
        database.Entry(row).CurrentValues.SetValues(ticket);
        Assert.True(await owner.CommitAsync());
        return true;
    });

    /// <summary>Rereads every field from a fresh runtime scope; status-only or partial metadata restoration cannot satisfy this oracle.</summary>
    internal Task<bool> RequireOriginalAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        Require(context, database.Entry(await database.Set<BrowserContext>().SingleAsync(TestContext.Current.CancellationToken)).CurrentValues);
        Require(intent, database.Entry(await database.Set<BrowserLoginIntent>().SingleAsync(TestContext.Current.CancellationToken)).CurrentValues);
        Require(ticket, database.Entry(await database.Set<AdminSessionTicket>().SingleAsync(TestContext.Current.CancellationToken)).CurrentValues);
        return true;
    });

    /// <summary>Compares blobs by content and other mapped values by exact type equality, without logging any protected ticket material.</summary>
    private static void Require(PropertyValues expected, PropertyValues actual)
    {
        Assert.Equal(expected.Properties.Select(value => value.Name), actual.Properties.Select(value => value.Name));
        foreach (var property in expected.Properties)
        {
            var left = expected[property.Name];
            var right = actual[property.Name];
            Assert.True(left is byte[] bytes ? right is byte[] other && bytes.SequenceEqual(other) : Equals(left, right));
        }
    }
}
