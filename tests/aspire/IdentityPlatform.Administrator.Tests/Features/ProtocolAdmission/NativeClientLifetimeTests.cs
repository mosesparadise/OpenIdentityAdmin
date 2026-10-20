using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires durable single-use callback state at real expiration equality and under simultaneous independent-instance delivery.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientLifetimeTests(BootstrapFixture fixture)
{
    private const int AttemptCount = 2;
    /// <summary>A positive native baseline precedes either two competing deliveries or expiration at the exact independently stored deadline.</summary>
    [Theory]
    [InlineData("PostgreSql", false, 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", false, 301, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, 301, Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurableCallback_When_ExpirationOrConcurrentDeliveryOccurs_Then_ConsumptionBoundsTokenExchange(string provider, bool concurrent, int expiredSeconds)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = NativeClientInteroperabilityTests.Load(material);
        using var secondKeys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(firstKeys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        var winnerProbe = new NativeClientCallbackRace();
        var peerProbe = new NativeClientCallbackRace();
        await using var first = new NativeClientHttp(database, issuer, profile, firstKeys, winnerProbe.Configure);
        await using var second = new NativeClientHttp(database, issuer, profile, secondKeys, peerProbe.Configure);
        await BaselineAsync(issuer, first, second);
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        if (!concurrent)
        {
            var expiry = await ExpirationAsync(first);
            Assert.True(expiry > issuer.Account.Journey.Clock.Now);
            if (expiredSeconds > 0)
            {
                Assert.True(TimeSpan.FromSeconds(expiredSeconds) > first.Services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue.TokenValidationParameters.ClockSkew);
            }
            issuer.Account.Journey.Clock.Now = expiry.AddSeconds(expiredSeconds);
        }
        var results = concurrent
            ? await NativeClientCallbackRace.InvokeAsync(database, first, second, winnerProbe, peerProbe, callback, challenge.Cookie)
            : new[] { await first.InvokeAsync(callback, challenge.Cookie), await second.InvokeAsync(callback, challenge.Cookie) };
        await RequireOutcomesAsync(results, concurrent, first, second);
    }

    /// <summary>Requires the designated winner and blocked peer outcomes together with exact sends and independently committed state.</summary>
    private static async Task RequireOutcomesAsync(NativeClientHttpResult[] results, bool concurrent, NativeClientHttp first, NativeClientHttp second)
    {
        for (var index = 0; index < results.Length; index++)
        {
            var result = results[index];
            NativeClientInteroperabilityTests.RequireClean(result);
            if (!concurrent && index > 0)
            {
                NativeClientDenial.RequireReplay(result);
            }
            else
            {
                Assert.Equal(result.Authentication?.Succeeded == true ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Status);
            }
        }
        Assert.Equal(concurrent, results[0].Authentication?.Succeeded == true);
        Assert.False(results[1].Authentication?.Succeeded == true);
        Assert.Equal(concurrent ? AttemptCount : 1, first.Bridge.TokenSends + second.Bridge.TokenSends);
        Assert.Equal(AttemptCount, first.Bridge.ParSends + second.Bridge.ParSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: AttemptCount, redeemed: AttemptCount);
    }

    /// <summary>Proves actual account/private-key exchange before a negative boundary can be credited.</summary>
    private static async Task BaselineAsync(AdministratorProtocolScenario issuer, NativeClientHttp first, NativeClientHttp second)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var accepted = await second.InvokeAsync(callback, challenge.Cookie);
        NativeClientInteroperabilityTests.RequireClean(accepted);
        NativeClientInteroperabilityTests.RequireAccepted(accepted);
        Assert.True(accepted.Authentication?.Succeeded == true);
        Assert.Equal(1, second.Bridge.TokenSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
    }

    /// <summary>Reads the real persisted expiration from a fresh Sessions scope instead of assuming a timeout or sleeping.</summary>
    private static Task<DateTimeOffset> ExpirationAsync(NativeClientHttp client) =>
        new ProtocolStorageRequest(client.Services.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken)
            .RunAsync(client.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                var expiry = await scope.Owner.RunAsync(current => new ValueTask<DateTime?>(scope.Owner.Database
                    .Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Status == OpenIddictConstants.Statuses.Valid)
                    .Select(row => row.ExpirationDate).SingleAsync(current)), token);
                Assert.NotNull(expiry);
                return new DateTimeOffset(DateTime.SpecifyKind(expiry.Value, DateTimeKind.Utc));
            });
}
