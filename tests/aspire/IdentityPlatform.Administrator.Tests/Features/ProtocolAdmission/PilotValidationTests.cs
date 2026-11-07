using System.Data.Common;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Executes native pilot validation through both real HTTPS composition paths and real provider runtime principals.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotValidationTests(BootstrapFixture fixture)
{
    /// <summary>Valid issuance and its first subsequent replay each acquire a distinct fresh gate scope and preserve native revocation.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PilotHostComposition_When_CodeExchangesThenReplays_Then_FreshGateValidationPreservesNativeLifecycle(string provider, bool standalone)
    {
        var probe = new PilotValidationProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: services => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(probe)),
            clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        using var winner = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        using var body = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await pilot.Client.ValidateAsync(body.RootElement, flow, TestContext.Current.CancellationToken));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        using var replay = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await InitialPilotReplayEvidence.RequireRejectedAsync(replay);
        await using var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.True(await manager.HasStatusAsync(entry, OpenIddictConstants.Statuses.Revoked, TestContext.Current.CancellationToken));
        Assert.Equal(2, probe.Contexts.Count);
        Assert.Equal(2, probe.Acquisitions);
    }
    /// <summary>A genuine protected code remains unconsumed when its separately committed authorization has lost native validity.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ValidCodeWithRevokedAuthorization_When_FreshPilotValidationRuns_Then_NoConsumptionOrGenerationOccurs(string provider, bool standalone)
    {
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, TestContext.Current.CancellationToken);
        var code = await PilotRevokedAuthorization.RevokeAsync(environment, identity.Item1);
        using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await InitialPilotReplayEvidence.RequireRejectedAsync(response);
        await PilotRevokedAuthorization.RequireUnconsumedAsync(environment, code);
    }

}

/// <summary>Counts actual successful transaction-bound gate statements and independently captures native context identity.</summary>
internal sealed class PilotValidationProbe : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Gate = "PilotProtocolGate";

    internal HashSet<Guid> Contexts { get; } = [];
    internal int Acquisitions
    {
        get; private set;
    }

    /// <summary>Observes only the coordination update; unrelated SQL and failed acquisitions cannot satisfy the assertion.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Gate, StringComparison.Ordinal))
        {
            Assert.NotNull(command.Transaction);
            Assert.Equal(1, result);
            Contexts.Add(eventData.Context!.ContextId.InstanceId);
            Acquisitions++;
        }
        return ValueTask.FromResult(result);
    }
}
