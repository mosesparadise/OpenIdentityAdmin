using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves explicit native HTTP cancellation before issuer protocol processing cannot retry or create a successful exchange.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientCancellationTests(BootstrapFixture fixture)
{
    private const int TotalStates = 2;

    /// <summary>Actual HTTPS cancellation is observed before cleanup, after client state creation/redemption and before any new issuer write.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualNativeHttps_When_ParOrTokenRequestIsExplicitlyCancelled_Then_ObservedDisconnectCannotRetryOrAuthenticate(string provider, bool tokenExchange)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = NativeClientInteroperabilityTests.Load(material);
        using var secondKeys = NativeClientInteroperabilityTests.Load(material);
        await using var host = await NativeIssuerHttpsHost.StartAsync();
        var profile = NativeClientInteroperabilityTests.Profile(firstKeys, host.Issuer);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration, host.Issuer);
        host.Bind(issuer);
        try
        {
            await using var database = await fixture.LeaseSessionsAsync(provider);
            await using var first = new NativeClientHttp(database, issuer, profile, firstKeys, network: true);
            await using var second = new NativeClientHttp(database, issuer, profile, secondKeys, network: true);
            await BaselineAsync(host, issuer, first, second);
            await CancelAsync(host, issuer, first, second, tokenExchange);
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Warms both real clients and requires a complete positive exchange over the unchanged trusted native HTTP stack.</summary>
    private static async Task BaselineAsync(NativeIssuerHttpsHost host, AdministratorProtocolScenario issuer, NativeClientHttp first, NativeClientHttp second)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        NativeClientInteroperabilityTests.RequireAccepted(await second.InvokeAsync(callback, challenge.Cookie));
        await host.DrainAsync();
        Assert.Equal(1, host.ParSends);
        Assert.Equal(1, host.TokenSends);
        Assert.Equal(0, host.Failures);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
    }

    /// <summary>Counts the intercepted request independently and requires unchanged native token/authorization rows and no successful client publication.</summary>
    private static async Task CancelAsync(NativeIssuerHttpsHost host, AdministratorProtocolScenario issuer,
        NativeClientHttp first, NativeClientHttp second, bool tokenExchange)
    {
        var challenge = tokenExchange ? await NativeClientInteroperabilityTests.ChallengeAsync(first) : null;
        var callback = challenge is null ? null : await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var before = await NativeAccountPersistence.CountAsync(issuer);
        var pars = host.ParSends;
        var tokens = host.TokenSends;
        var denied = await NativeClientCancellation.InvokeAsync(host, tokenExchange ? second : first, callback, challenge?.Cookie);
        Assert.True(denied.Aborted);
        Assert.False(denied.Authentication?.Succeeded == true);
        Assert.Equal(0, denied.BodyLength);
        Assert.True(string.IsNullOrEmpty(denied.Location));
        Assert.Empty(denied.Cookies);
        Assert.True(denied.Failure is null or OperationCanceledException, denied.Failure?.GetType().FullName);
        Assert.Equal(pars + (tokenExchange ? 0 : 1), host.ParSends);
        Assert.Equal(tokens + (tokenExchange ? 1 : 0), host.TokenSends);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
        Assert.Equal(0, host.Failures);
        Assert.Equal(0, host.RedirectedSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, TotalStates, tokenExchange ? TotalStates : 1);
        if (callback is not null)
        {
            NativeClientDenial.RequireReplay(await first.InvokeAsync(callback, challenge!.Cookie));
            await host.DrainAsync();
            Assert.Equal(tokens + 1, host.TokenSends);
            Assert.Equal(pars, host.ParSends);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
        }
    }
}
