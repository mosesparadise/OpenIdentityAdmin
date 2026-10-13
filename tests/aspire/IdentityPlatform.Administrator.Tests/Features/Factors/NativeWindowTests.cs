using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Checks each native Identity wall-clock window edge with an independent RFC oracle and one verification attempt.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeWindowTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private const int FirstOffset = -3;
    private const int OffsetCount = 7;
    private const int StepSeconds = 30;
    private const int StableSeconds = 15;
    private const int AcceptedOffset = 2;
    private const string ProviderTrait = "Provider";

    /// <summary>Enumerates every accepted counter and both adjacent rejected counters independently on both providers.</summary>
    public static IEnumerable<TheoryDataRow<string, int>> Cases =>
        from provider in Providers
        from offset in Enumerable.Range(FirstOffset, OffsetCount)
        select new TheoryDataRow<string, int>(provider, offset).WithTrait(ProviderTrait, provider);

    /// <summary>Native UTC verification accepts each documented counter and rejects both adjacent outside-window values.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_NativeAuthenticator_When_EachClockWindowOffsetIsVerified_Then_OnlyFiveCurrentCountersAreAccepted(string provider, int offset)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        await StableWindowAsync(state.Token);
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = FactorOracle.Code(before + offset * StepSeconds);
        var accepted = offset is >= -AcceptedOffset and <= AcceptedOffset;
        var proof = await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System,
            interaction.Id, code, state.Token);
        Assert.Equal(before / StepSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds);
        FactorOracle.RequireWindow(FactorScenario.Seed, code, before, accepted);
        Assert.Equal(accepted, proof is not null);
    }

    /// <summary>Schedules the first and only verification in the first half of a counter; never retries an authentication result.</summary>
    private static async Task StableWindowAsync(CancellationToken token)
    {
        var remaining = StepSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % StepSeconds;
        if (remaining <= StableSeconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(remaining + 1), token);
        }
    }
}
