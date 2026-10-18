using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Injects real awaited read/disposal mutations only after a genuine two-instance native callback has passed.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientCallbackMutationTests(BootstrapFixture fixture)
{
    /// <summary>Each reached mutation withholds callback output and token exchange; independent rows distinguish pre-consumption rollback from post-commit denial.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ValidatedNativeCallback_When_ActualReadOrChildDisposalChangesState_Then_NoSubstitutedExchangeOccurs(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = NativeClientInteroperabilityTests.Load(material);
        using var secondKeys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(firstKeys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        var fault = new NativeClientCallbackFault();
        await using var first = new NativeClientHttp(database, issuer, profile, firstKeys);
        await using var second = new NativeClientHttp(database, issuer, profile, secondKeys, fault.Configure);
        await BaselineAsync(first, second, issuer);
        var total = 1;
        var redeemed = 1;
        foreach (var mode in Enum.GetValues<NativeClientCallbackFault.Kind>().Where(value => value != NativeClientCallbackFault.Kind.None))
        {
            var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
            var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
            fault.Mode = mode;
            fault.Reads = fault.Mutations = 0;
            NativeClientHttpResult denied;
            try
            {
                denied = await second.InvokeAsync(callback, challenge.Cookie);
            }
            finally { fault.Mode = NativeClientCallbackFault.Kind.None; fault.Context = null; }
            Assert.Equal(1, fault.Reads);
            Assert.Equal(1, fault.Mutations);
            RequireWithheld(denied);
            total++;
            if (mode >= NativeClientCallbackFault.Kind.CleanupClone)
            {
                redeemed++;
            }
            await NativeClientInteroperabilityTests.RequireStatesAsync(first, total, redeemed);
            Assert.Equal(1, first.Bridge.TokenSends + second.Bridge.TokenSends);
        }
    }

    /// <summary>Proves the unchanged native path succeeds before arming a single fault at a time.</summary>
    private static async Task BaselineAsync(NativeClientHttp first, NativeClientHttp second, AdministratorProtocolScenario issuer)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var result = await second.InvokeAsync(callback, challenge.Cookie);
        NativeClientInteroperabilityTests.RequireClean(result);
        Assert.Equal(StatusCodes.Status200OK, result.Status);
        Assert.True(result.Authentication?.Succeeded == true);
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
        Assert.Equal(1, second.Bridge.TokenSends);
    }

    /// <summary>Asserts only finite failure/output properties so an unexpected credential-bearing response cannot enter reports.</summary>
    private static void RequireWithheld(NativeClientHttpResult result)
    {
        var expectedFailure = result.Failure?.GetType() == typeof(InvalidOperationException);
        Assert.True(expectedFailure);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Status);
        Assert.False(result.Authentication?.Succeeded == true);
        Assert.Equal(0, result.BodyLength);
        var cookies = result.Cookies.Length;
        Assert.Equal(0, cookies);
        Assert.True(string.IsNullOrEmpty(result.Location));
    }
}
