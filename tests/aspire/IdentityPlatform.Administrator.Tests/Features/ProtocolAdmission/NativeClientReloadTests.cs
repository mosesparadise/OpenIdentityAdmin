using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Reloads protected credentials and independent client roots over actual persisted pending state, without claiming process restart.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientReloadTests(BootstrapFixture fixture)
{
    private const string Section = "ClientCredentials";
    private const string PreviousSigning = "ClientCredentials:StateSigning:Previous:";
    private const string PreviousEncryption = "ClientCredentials:StateEncryption:Previous:";
    private const string InvalidTokenPrefix = "InvalidToken/";
    private const int PendingTotal = 2;
    private const int ReplacedTotal = 3;
    private const int BaselineParSends = 2;

    /// <summary>Disposed instances leave usable durable state; missing validation or decryption keys deny independently before restored retained keys complete it.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedPendingCallback_When_ClientRootsAndStateKeysAreReloaded_Then_OnlyCapableKeysCompleteOneExchange(string provider, bool replace)
    {
        using var material = new NativeClientCredentialFixture();
        var original = material.Settings();
        NativeClientProfile profile;
        using (var keys = Load(original))
        {
            profile = NativeClientInteroperabilityTests.Profile(keys);
        }
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        var pending = await PrepareAsync(database, issuer, profile, original, baseline: true);
        var replacement = replace ? material.RotatedSettings() : original;
        if (replace)
        {
            foreach (var omitted in new[] { PreviousSigning, PreviousEncryption })
            {
                var incomplete = replacement.Where(pair => !pair.Key.StartsWith(omitted, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                await RequireMissingKeyAsync(database, issuer, profile, incomplete, pending, PendingTotal);
            }
        }
        await CompleteAndReplayAsync(database, issuer, profile, replacement, pending, PendingTotal);
        if (replace)
        {
            var fresh = await PrepareAsync(database, issuer, profile, replacement, baseline: false);
            await RequireMissingKeyAsync(database, issuer, profile, original, fresh, ReplacedTotal);
            await CompleteAndReplayAsync(database, issuer, profile, replacement, fresh, ReplacedTotal);
        }
    }

    /// <summary>Requires actual normal interoperability before persisting a fresh challenge; all original consumers and imported keys leave scope before returning.</summary>
    private static async Task<(Uri Callback, string Cookie)> PrepareAsync(BootstrapSessions database, AdministratorProtocolScenario issuer,
        NativeClientProfile profile, Dictionary<string, string?> settings, bool baseline)
    {
        using var keys = Load(settings);
        await using var client = new NativeClientHttp(database, issuer, profile, keys);
        if (baseline)
        {
            var first = await NativeClientInteroperabilityTests.ChallengeAsync(client);
            var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, first, cookie: true);
            NativeClientInteroperabilityTests.RequireAccepted(await client.InvokeAsync(callback, first.Cookie));
            await NativeClientInteroperabilityTests.RequireStatesAsync(client, total: 1, redeemed: 1);
        }
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(client);
        var pending = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        Assert.Equal(baseline ? BaselineParSends : 1, client.Bridge.ParSends);
        Assert.Equal(baseline ? 1 : 0, client.Bridge.TokenSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(client, baseline ? PendingTotal : ReplacedTotal,
            redeemed: baseline ? 1 : PendingTotal);
        return (pending, challenge.Cookie);
    }

    /// <summary>Crypto denial leaves pending state usable, proves no token request or issuer issuance, and emits no staged native error material.</summary>
    private static async Task RequireMissingKeyAsync(BootstrapSessions database, AdministratorProtocolScenario issuer, NativeClientProfile profile,
        Dictionary<string, string?> settings, (Uri Callback, string Cookie) pending, int total)
    {
        using var keys = Load(settings);
        await using var client = new NativeClientHttp(database, issuer, profile, keys);
        var issued = await NativeAccountPersistence.AccessTokensAsync(issuer);
        var denied = await client.InvokeAsync(pending.Callback, pending.Cookie);
        NativeClientDenial.RequireSuppressed(denied);
        Assert.StartsWith(InvalidTokenPrefix, denied.Rejection, StringComparison.Ordinal);
        Assert.Equal(0, client.Bridge.TokenSends);
        Assert.Equal(0, client.Bridge.ParSends);
        Assert.Equal(issued, await NativeAccountPersistence.AccessTokensAsync(issuer));
        await NativeClientInteroperabilityTests.RequireStatesAsync(client, total, redeemed: total - 1);
    }

    /// <summary>Fresh capable credentials complete once; a further fully capable root proves durable replay denial rather than a missing-key failure.</summary>
    private static async Task CompleteAndReplayAsync(BootstrapSessions database, AdministratorProtocolScenario issuer, NativeClientProfile profile,
        Dictionary<string, string?> settings, (Uri Callback, string Cookie) pending, int total)
    {
        var issued = await NativeAccountPersistence.AccessTokensAsync(issuer);
        using (var keys = Load(settings))
        {
            await using var client = new NativeClientHttp(database, issuer, profile, keys);
            NativeClientInteroperabilityTests.RequireAccepted(await client.InvokeAsync(pending.Callback, pending.Cookie));
            Assert.Equal(1, client.Bridge.TokenSends);
            Assert.Equal(0, client.Bridge.ParSends);
            await NativeClientInteroperabilityTests.RequireStatesAsync(client, total, redeemed: total);
        }
        Assert.Equal(issued + 1, await NativeAccountPersistence.AccessTokensAsync(issuer));
        using var replayKeys = Load(settings);
        await using var replay = new NativeClientHttp(database, issuer, profile, replayKeys);
        NativeClientDenial.RequireReplay(await replay.InvokeAsync(pending.Callback, pending.Cookie));
        Assert.Equal(0, replay.Bridge.TokenSends);
        Assert.Equal(0, replay.Bridge.ParSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(replay, total, redeemed: total);
        Assert.Equal(issued + 1, await NativeAccountPersistence.AccessTokensAsync(issuer));
    }

    /// <summary>Imports new certificate objects from the same protected files for every independently disposed root.</summary>
    private static NativeClientCredentials Load(Dictionary<string, string?> settings) => NativeClientCredentials.Load(
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection(Section), DateTimeOffset.UtcNow);
}
