using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Demonstrates that exact restored code/state images resurrect a consumed callback and explicit isolated-data invalidation prevents reuse.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRestoreConsequenceTests(BootstrapFixture fixture)
{
    private const string RevokedState = "InvalidToken/ID2019";
    private const int ExchangeRows = 2;

    /// <summary>Replays genuine native artifacts after exact row restoration, then repeats restoration plus explicit invalidation and requires denial.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConsumedCodeAndStateImages_When_RestoredThenInvalidated_Then_ResurrectionIsDemonstratedAndCallbackCannotIssueAgain(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = NativeClientInteroperabilityTests.Load(material);
        using var peerKeys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(firstKeys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var first = new NativeClientHttp(database, issuer, profile, firstKeys);
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var state = await NativeTokenSnapshot.CaptureAsync(first.Services, clientState: true);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        var fields = QueryHelpers.ParseQuery(callback.Query);
        Assert.True(fields.TryGetValue(OpenIddictConstants.Parameters.Code, out var code) && code.Count == 1 && !string.IsNullOrEmpty(code[0]));
        var principal = await NativeAccountFlow.ValidateAsync(issuer, code[0]!, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var token = await NativeTokenSnapshot.CaptureAsync(issuer.Services, clientState: false, principal.GetTokenId()!);
        await RequireAcceptedAsync(first, issuer, callback, challenge.Cookie);
        await using var peer = new NativeClientHttp(database, issuer, profile, peerKeys);
        NativeClientDenial.RequireReplay(await peer.InvokeAsync(callback, challenge.Cookie));
        Assert.Equal(0, peer.Bridge.TokenSends);
        await RestoreAsync(issuer, peer, token, state);
        await using var resurrected = new NativeClientHttp(database, issuer, profile, peerKeys);
        await RequireAcceptedAsync(resurrected, issuer, callback, challenge.Cookie);
        Assert.Equal(1, resurrected.Bridge.TokenSends);
        await RestoreAsync(issuer, resurrected, token, state);
        await NativeRestoreInvalidation.ApplyAsync(issuer);
        await state.RevokeAsync(resurrected.Services);
        await using var isolated = new NativeClientHttp(database, issuer, profile, peerKeys);
        await RequireInvalidatedAsync(issuer, isolated, token, state, callback, challenge.Cookie);
        Assert.Equal(1, first.Bridge.ParSends);
        Assert.Equal(1, first.Bridge.TokenSends);
        Assert.Equal(0, peer.Bridge.ParSends);
        Assert.Equal(0, peer.Bridge.TokenSends);
        Assert.Equal(0, isolated.Bridge.ParSends);
        Assert.Equal(0, isolated.Bridge.TokenSends);
    }

    /// <summary>Restores quiescent real rows sequentially, then compares every private scalar field through independent fresh scopes.</summary>
    private static async Task RestoreAsync(AdministratorProtocolScenario issuer, NativeClientHttp client, NativeTokenSnapshot code, NativeTokenSnapshot state)
    {
        await code.RestoreAsync(issuer.Services);
        await state.RestoreAsync(client.Services);
        await code.RequireOriginalAsync(issuer.Services);
        await state.RequireOriginalAsync(client.Services);
    }

    /// <summary>Requires actual native authentication and exact issuer row growth, including the intentionally unsafe restored-row positive control.</summary>
    private static async Task RequireAcceptedAsync(NativeClientHttp client, AdministratorProtocolScenario issuer, Uri callback, string cookie)
    {
        var before = await NativeAccountPersistence.CountAsync(issuer);
        var result = await client.InvokeAsync(callback, cookie);
        NativeClientInteroperabilityTests.RequireAccepted(result);
        Assert.True(result.Authentication!.Principal!.GetClaim(OpenIddictConstants.Claims.Subject) == issuer.Account.ActorId);
        Assert.Equal(0, result.BodyLength);
        Assert.True(result.Location.Length == 0);
        foreach (var value in result.Cookies)
        {
            NativeClientInteroperabilityTests.RequireCookie(value, attributes: false);
        }
        Assert.Equal((before.Tokens + ExchangeRows, before.Authorizations), await NativeAccountPersistence.CountAsync(issuer));
    }

    /// <summary>Shows actual callback rejection without another exchange and separately proves all intended persisted invalidation effects remain intact.</summary>
    private static async Task RequireInvalidatedAsync(AdministratorProtocolScenario issuer, NativeClientHttp client,
        NativeTokenSnapshot code, NativeTokenSnapshot state, Uri callback, string cookie)
    {
        var before = await NativeAccountPersistence.CountAsync(issuer);
        var result = await client.InvokeAsync(callback, cookie);
        NativeClientDenial.RequireSuppressed(result);
        Assert.Equal(RevokedState, result.Rejection);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
        await NativeRestoreInvalidation.RequireAsync(issuer);
        await code.RequireStatusAsync(issuer.Services, OpenIddictConstants.Statuses.Revoked);
        await state.RequireStatusAsync(client.Services, OpenIddictConstants.Statuses.Revoked);
    }
}
