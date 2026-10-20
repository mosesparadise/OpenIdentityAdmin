using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves actual Sessions runtime read denial cannot create or redeem native state, and precise permission recovery preserves pending callbacks.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientDatabaseFailureTests(BootstrapFixture fixture)
{
    private const int BaselineStates = 1;
    private const int CompletedStates = 2;

    /// <summary>Independent provider-denial evidence and a real positive exchange bracket the challenge/callback failure on the disposable table.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualSessionsReadDenied_When_NativeChallengeOrCallbackRuns_Then_NoExchangeOccursAndRestoredAccessPreservesPendingState(
        string provider, bool callbackFailure)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(keys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var client = new NativeClientHttp(database, issuer, profile, keys);
        var baseline = await NativeClientInteroperabilityTests.ChallengeAsync(client);
        var baselineCallback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, baseline, cookie: true);
        NativeClientInteroperabilityTests.RequireAccepted(await client.InvokeAsync(baselineCallback, baseline.Cookie));
        await database.Access.ReadAsync(TestContext.Current.CancellationToken);
        var pending = callbackFailure ? await NativeClientInteroperabilityTests.ChallengeAsync(client) : null;
        var callback = pending is null ? null : await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, pending, cookie: true);
        await RequireFailureAsync(database, issuer, client, callback, pending?.Cookie);
        await NativeClientInteroperabilityTests.RequireStatesAsync(client, callbackFailure ? CompletedStates : BaselineStates, BaselineStates);
        using var recoveredKeys = NativeClientInteroperabilityTests.Load(material);
        await using var recovered = new NativeClientHttp(database, issuer, profile, recoveredKeys);
        if (pending is null)
        {
            pending = await NativeClientInteroperabilityTests.ChallengeAsync(recovered);
            callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, pending, cookie: true);
        }
        NativeClientInteroperabilityTests.RequireAccepted(await recovered.InvokeAsync(callback, pending.Cookie));
        Assert.Equal(1, recovered.Bridge.TokenSends);
        Assert.Equal(callbackFailure ? 0 : 1, recovered.Bridge.ParSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(recovered, CompletedStates, CompletedStates);
        NativeClientDenial.RequireReplay(await client.InvokeAsync(callback, pending.Cookie));
        Assert.Equal(1, client.Bridge.TokenSends);
    }

    /// <summary>Requires the actual callback/challenge failure to contain the same provider permission denial, without accepting a generic unavailable result.</summary>
    private static async Task RequireFailureAsync(BootstrapSessions database, AdministratorProtocolScenario issuer,
        NativeClientHttp client, Uri? callback, string? cookie)
    {
        var before = await NativeAccountPersistence.CountAsync(issuer);
        var pars = client.Bridge.ParSends;
        var tokens = client.Bridge.TokenSends;
        await NativeClientStateFailure.DuringDeniedReadAsync(database.Access, async () =>
        {
            var denied = await client.InvokeAsync(callback, cookie);
            Assert.True(database.Access.IsDenied(denied.Failure), denied.Failure?.GetType().FullName);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, denied.Status);
            Assert.False(denied.Authentication?.Succeeded == true);
            Assert.Equal(0, denied.BodyLength);
            Assert.True(string.IsNullOrEmpty(denied.Location));
            Assert.True(denied.Cookies.Length == 0);
            Assert.Equal(pars, client.Bridge.ParSends);
            Assert.Equal(tokens, client.Bridge.TokenSends);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
        });
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
    }
}
