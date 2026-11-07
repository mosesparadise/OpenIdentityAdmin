using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks native write rollback when request cancellation or strict transaction expiry is observed after actual work completes.</summary>
public sealed class PilotGateLifetimeTests
{
    private const string Client = "synthetic-pilot-expired-phase";

    /// <summary>Advances the original monotonic budget without firing timers, so completed native tasks cannot hide exact expiry.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_GatePhaseWithNativeWrite_When_OriginalLifetimeEnds_Then_ActualCleanupRollsBackBeforeReturn(string provider, bool cancel)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var clock = new PilotPhaseClock();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PilotProtocolTransaction.RunAsync(
            fixture.Services.GetRequiredService<IServiceScopeFactory>(), clock, async (services, token) =>
            {
                await services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = Client,
                    ClientType = OpenIddictConstants.ClientTypes.Public
                }, token);
                if (cancel)
                {
                    await request.CancelAsync();
                }
                else
                {
                    clock.Expire();
                }
            }, request.Token).AsTask());
        await using var observer = fixture.Scope();
        Assert.Null(await observer.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(Client, fixture.Token));
    }
}

/// <summary>Controls monotonic observation independently of real timer delivery to expose late completed-task admission.</summary>
internal sealed class PilotPhaseClock : TimeProvider
{
    private const int PhaseSeconds = 5;
    private long ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Returns the one original transaction timeline.</summary>
    public override long GetTimestamp() => ticks;

    /// <summary>Reaches the independent five-second contract boundary without waiting for scheduling or timer delivery.</summary>
    internal void Expire() => ticks += TimeSpan.FromSeconds(PhaseSeconds).Ticks;
}
