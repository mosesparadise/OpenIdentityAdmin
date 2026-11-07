using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Requires native database contention and cancellation while independent authority reads remain unblocked.</summary>
public sealed class PilotGateContentionTests
{
    private const int ObserveSeconds = 2;
    /// <summary>Uses exact native session IDs and an ordered release, preserving the production transaction budget without sleeps.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_HeldPilotGate_When_AnotherPhaseWaits_Then_DatabaseSerializesWithoutBlockingAuthorityReads(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = database.Database.GetConnectionString()!;
        var probe = new PilotGateSessionProbe();
        await using var root = new ServiceCollection().AddLogging().AddPlatformPersistence(TestDatabaseProviders.Resolve(provider), connection, connection)
            .AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(probe)).BuildServiceProvider(validateScopes: true);
        var factory = root.GetRequiredService<IServiceScopeFactory>();
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = PilotProtocolTransaction.RunAsync(factory, TimeProvider.System, async (services, token) =>
        {
            entered.SetResult(NativeLockObservation.Session(services.GetRequiredService<PlatformDbContext>().Database.GetDbConnection()));
            await release.Task.WaitAsync(token);
        }, fixture.Token).AsTask();
        Task? contender = null;
        try
        {
            var session = await entered.Task.WaitAsync(TimeSpan.FromSeconds(ObserveSeconds), fixture.Token);
            Assert.Empty(await database.Set<PilotManifest>().AsNoTracking().ToListAsync(fixture.Token));
            probe.Armed = true;
            contender = PilotProtocolTransaction.RunAsync(factory, TimeProvider.System, (_, _) => ValueTask.CompletedTask, fixture.Token).AsTask();
            var peer = await probe.Session.Task.WaitAsync(TimeSpan.FromSeconds(ObserveSeconds), fixture.Token);
            Assert.NotEqual(session, peer);
            await new NativeLockObservation(provider, connection).WaitAsync(peer, session, fixture.Token);
            Assert.False(contender.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(contender is null ? [holder] : [holder, contender]);
        }
        Assert.Single(await database.Set<PilotProtocolGate>().AsNoTracking().ToListAsync(fixture.Token));
    }
}

/// <summary>Captures the actual competing UPDATE connection immediately before native lock acquisition.</summary>
internal sealed class PilotGateSessionProbe : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Gate = "PilotProtocolGate";

    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource<int> Session { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Never infers contention from command dispatch; the caller queries the engine's exact wait relationship.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Gate, StringComparison.Ordinal))
        {
            Assert.NotNull(command.Transaction);
            Session.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }
}
