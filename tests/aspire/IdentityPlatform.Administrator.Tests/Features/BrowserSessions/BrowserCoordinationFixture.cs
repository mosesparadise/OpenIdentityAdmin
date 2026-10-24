using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Runs coordination with real Sessions-only credentials, separately disposed contexts and the existing protected B1 codec.</summary>
internal sealed class BrowserCoordinationFixture(BootstrapSessions lease) : IAsyncDisposable
{
    private const int SelectorBytes = 32;

    internal BootstrapSessions Lease => lease;
    internal BffSessionFixture Material { get; } = new();
    internal BffSessionClock Clock { get; } = new(BffSessionFixture.Start);
    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Uses native Migrator/provisioning on a new database owned only by this test.</summary>
    internal static async Task<BrowserCoordinationFixture> CreateAsync(BootstrapFixture fixture, string provider) =>
        new(await fixture.LeaseSessionsAsync(provider));

    /// <summary>Allocates a test digest independently of the production selector helper.</summary>
    internal static string Digest() => Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(SelectorBytes)));

    /// <summary>Builds a fresh encrypted ticket using actual B1 policy and retained protected key material.</summary>
    internal AdminSessionTicket Ticket()
    {
        var row = BffSessionFixture.Row(Clock.GetUtcNow());
        using var protection = Material.Protection();
        row.ProtectedTicket = new SessionTicketCodec(protection.Provider).Protect(row, BffSessionFixture.Ticket());
        return row;
    }

    /// <summary>Creates an independent runtime root and context per operation and disposes both before returning a result.</summary>
    internal async Task<T> RunAsync<T>(Func<AdminSessionDbContext, SessionTicketCodec, Task<T>> action, params IInterceptor[] interceptors)
    {
        var services = lease.AddPersistence(new ServiceCollection().AddLogging(logging => logging.ClearProviders()));
        services.AddDbContext<AdminSessionDbContext>(options => options.AddInterceptors(interceptors));
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        using var protection = Material.Protection();
        return await action(scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>(), new SessionTicketCodec(protection.Provider));
    }

    /// <summary>Starts an explicit anonymous generation through the actual production coordinator.</summary>
    internal Task<long?> StartAsync(string browser, string intent, long generation = 0, string? actor = null, params IInterceptor[] faults) =>
        RunAsync((database, codec) => new BrowserLoginStart(database, Clock, codec).StartAsync(browser, intent, generation, actor, Token), faults);

    /// <summary>Reserves one native attempt without simulating native token exchange or claiming its acceptance.</summary>
    internal Task<bool> ReserveAsync(string browser, string intent, long generation, string reservation, params IInterceptor[] faults) =>
        RunAsync((database, codec) => new BrowserLoginCompletion(database, Clock, codec).ReserveAsync(browser, intent, generation, reservation, Token), faults);

    /// <summary>Exercises atomic persistence with a genuine protected ticket; this is not product callback evidence.</summary>
    internal Task<bool> EstablishAsync(string browser, string intent, long generation, string reservation, AdminSessionTicket ticket, params IInterceptor[] faults) =>
        RunAsync((database, codec) => new BrowserLoginCompletion(database, Clock, codec).EstablishAsync(browser, intent, generation, reservation, ticket, Token), faults);

    /// <summary>Runs the independent local revocation boundary with optional already validated cookie provenance.</summary>
    internal Task<long?> LogoutAsync(string browser, long generation, string? actor = null, string? correlation = null, params IInterceptor[] faults) =>
        RunAsync(async (database, codec) => (await new BrowserLogoutService(database, Clock, codec).LogoutAsync(browser, generation, actor, correlation, Token))?.Generation, faults);

    /// <summary>Checks only durable generation acceptance, which must be paired with normal B1 authentication by product orchestration.</summary>
    internal Task<bool> CurrentAsync(string browser, long generation, string ticket) =>
        RunAsync((database, codec) => new BrowserLoginCompletion(database, Clock, codec).IsCurrentAsync(browser, generation, ticket, Token));

    /// <summary>Hashes all owned coordination, protected payload and audit effects from a newly opened context.</summary>
    internal Task<string> SnapshotAsync() => RunAsync(async (database, _) =>
    {
        var state = new
        {
            Contexts = await database.Set<BrowserContext>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(Token),
            Intents = await database.Set<BrowserLoginIntent>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(Token),
            Logouts = await database.Set<BrowserLogoutContinuation>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(Token),
            Audits = await database.Set<BrowserSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(Token),
            Budget = await database.Set<BrowserSessionBudget>().AsNoTracking().ToArrayAsync(Token),
            Tickets = await database.Set<AdminSessionTicket>().AsNoTracking().OrderBy(row => row.Digest)
                .Select(row => new { Row = row, Payload = row.ProtectedTicket, row.RevokedAt }).ToArrayAsync(Token),
            TicketAudits = await database.Set<AdminSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(Token)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    });

    /// <summary>Drops the isolated database and then only this fixture's synthetic key material.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await lease.DisposeAsync();
        }
        finally { Material.Dispose(); }
    }
}
