using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Rejects authentic issuer-signed identity responses whose individual bindings violate the native client contract.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientIdentityTests(BootstrapFixture fixture)
{
    /// <summary>Normal and unchanged re-sign controls must authenticate before each signed nonce/issuer/audience/type/expiry violation is credited.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualSignedIdentityResponse_When_OneBindingChanges_Then_NativeClientDeniesAndConsumedStateCannotResubmit(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = NativeClientInteroperabilityTests.Load(material);
        using var secondKeys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(firstKeys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var first = new NativeClientHttp(database, issuer, profile, firstKeys);
        await using var second = new NativeClientHttp(database, issuer, profile, secondKeys);
        await AttemptAsync(first, second, issuer, fault: null, total: 1);
        var total = 1;
        foreach (var fault in Enum.GetValues<NativeIdentityResponse.Fault>())
        {
            await AttemptAsync(first, second, issuer, fault, ++total);
        }
    }

    /// <summary>Every independent flow proves one actual token send, committed issuer output and consumed client state, then replay denial without another send.</summary>
    private static async Task AttemptAsync(NativeClientHttp first, NativeClientHttp second, AdministratorProtocolScenario issuer,
        NativeIdentityResponse.Fault? fault, int total)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var issued = await NativeAccountPersistence.AccessTokensAsync(issuer);
        var rewrites = second.Bridge.IdentityRewrites;
        NativeClientHttpResult result;
        second.Bridge.IdentityFault = fault;
        try
        {
            result = await second.InvokeAsync(callback, challenge.Cookie);
        }
        finally { second.Bridge.IdentityFault = null; }
        NativeClientInteroperabilityTests.RequireClean(result);
        if (fault is null or NativeIdentityResponse.Fault.Unchanged)
        {
            NativeClientInteroperabilityTests.RequireAccepted(result);
        }
        else
        {
            Assert.Equal(StatusCodes.Status400BadRequest, result.Status);
            Assert.False(result.Authentication?.Succeeded == true);
        }
        Assert.Equal(total, second.Bridge.TokenSends);
        Assert.Equal(rewrites + (fault is null ? 0 : 1), second.Bridge.IdentityRewrites);
        Assert.Equal(issued + 1, await NativeAccountPersistence.AccessTokensAsync(issuer));
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total, redeemed: total);
        var replay = await first.InvokeAsync(callback, challenge.Cookie);
        NativeClientDenial.RequireReplay(replay);
        Assert.Equal(total, first.Bridge.TokenSends + second.Bridge.TokenSends);
    }
}
