using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises native TLS backchannels and post-commit response loss without replacing transport, trust or protocol serialization.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientHttpsTests(BootstrapFixture fixture)
{
    /// <summary>Real HTTPS succeeds before each reached loss, unavailable response or redirect proves single-send denial on both stores.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TrustedNativeHttps_When_ParOrTokenResponseIsLostOrRejected_Then_NoRetryOrRedirectCanCompleteAuthentication(string provider)
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
            await NativeClientTlsTrust.RequireUntrustedAsync(first.Services);
            var total = 1;
            var redeemed = 1;
            foreach (var fault in Enum.GetValues<NativeIssuerHttpsHost.Fault>().Where(value => value != NativeIssuerHttpsHost.Fault.None))
            {
                if (IsToken(fault))
                {
                    await TokenFailureAsync(host, issuer, first, second, fault);
                    redeemed++;
                }
                else
                {
                    await ParFailureAsync(host, issuer, first, fault);
                }
                await NativeClientInteroperabilityTests.RequireStatesAsync(second, ++total, redeemed);
                Assert.Equal(0, host.RedirectedSends);
                Assert.Equal(0, host.Failures);
            }
            Assert.Equal(2, host.Losses);
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Requires actual trusted TLS discovery/PAR/token and a successful independent-instance native callback before interventions.</summary>
    private static async Task BaselineAsync(NativeIssuerHttpsHost host, AdministratorProtocolScenario issuer, NativeClientHttp first, NativeClientHttp second)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var accepted = await second.InvokeAsync(callback, challenge.Cookie);
        NativeClientInteroperabilityTests.RequireClean(accepted);
        NativeClientInteroperabilityTests.RequireAccepted(accepted);
        Assert.True(accepted.Authentication?.Succeeded == true);
        await host.DrainAsync();
        Assert.Equal(0, host.Failures);
        Assert.Equal(1, host.ParSends);
        Assert.Equal(1, host.TokenSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
    }

    /// <summary>A PAR failure cannot expose a redirect; only the post-commit loss retains one actual native request entry.</summary>
    private static async Task ParFailureAsync(NativeIssuerHttpsHost host, AdministratorProtocolScenario issuer, NativeClientHttp client,
        NativeIssuerHttpsHost.Fault fault)
    {
        var before = await NativeAccountPersistence.CountAsync(issuer);
        var sends = host.ParSends;
        var tokens = host.TokenSends;
        host.Selected = fault;
        NativeClientHttpResult denied;
        try
        {
            denied = await client.InvokeAsync();
        }
        finally { host.Selected = NativeIssuerHttpsHost.Fault.None; }
        await host.DrainAsync();
        RequireDenied(denied);
        Assert.Equal(sends + 1, host.ParSends);
        Assert.Equal(tokens, host.TokenSends);
        var after = await NativeAccountPersistence.CountAsync(issuer);
        Assert.Equal(before.Tokens + (fault == NativeIssuerHttpsHost.Fault.ParLoss ? 1 : 0), after.Tokens);
        Assert.Equal(before.Authorizations, after.Authorizations);
    }

    /// <summary>Lost successful exchange retains committed issuance but never authenticates or resubmits; other transport denials issue nothing.</summary>
    private static async Task TokenFailureAsync(NativeIssuerHttpsHost host, AdministratorProtocolScenario issuer,
        NativeClientHttp first, NativeClientHttp second, NativeIssuerHttpsHost.Fault fault)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var before = await NativeAccountPersistence.AccessTokensAsync(issuer);
        var sends = host.TokenSends;
        var pars = host.ParSends;
        host.Selected = fault;
        NativeClientHttpResult denied;
        try
        {
            denied = await second.InvokeAsync(callback, challenge.Cookie);
        }
        finally { host.Selected = NativeIssuerHttpsHost.Fault.None; }
        await host.DrainAsync();
        RequireDenied(denied);
        Assert.Equal(sends + 1, host.TokenSends);
        Assert.Equal(pars, host.ParSends);
        var after = await NativeAccountPersistence.AccessTokensAsync(issuer);
        Assert.Equal(before + (fault == NativeIssuerHttpsHost.Fault.TokenLoss ? 1 : 0), after);
        var replay = await first.InvokeAsync(callback, challenge.Cookie);
        NativeClientDenial.RequireReplay(replay);
        await host.DrainAsync();
        Assert.Equal(sends + 1, host.TokenSends);
    }

    /// <summary>Uses a finite phase classification rather than string matching on secret-bearing native errors.</summary>
    private static bool IsToken(NativeIssuerHttpsHost.Fault fault) => fault is NativeIssuerHttpsHost.Fault.TokenLoss or
        NativeIssuerHttpsHost.Fault.TokenUnavailable or NativeIssuerHttpsHost.Fault.TokenRedirect;

    /// <summary>Checks denied publication without exposing protocol error descriptions, response bytes or credentials.</summary>
    private static void RequireDenied(NativeClientHttpResult denied)
    {
        Assert.True(denied.Status is StatusCodes.Status400BadRequest or StatusCodes.Status503ServiceUnavailable);
        Assert.False(denied.Authentication?.Succeeded == true);
        Assert.True(string.IsNullOrEmpty(denied.Location));
    }
}
