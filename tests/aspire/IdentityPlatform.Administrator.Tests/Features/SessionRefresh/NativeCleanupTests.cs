using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Characterizes native authenticated selector cleanup with real issuance and both providers, without asserting product B3 integration.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeCleanupTests(BootstrapFixture fixture)
{
    /// <summary>Only the retained pre-exchange intent is needed after the successful initial response is deliberately discarded.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_DiscardedInitialResponse_When_NativeClientCleansIntent_Then_ExactFamilyIsDeniedAndUnrelatedRefreshSurvives(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        var unrelated = await NativeCleanupFlow.CodeAsync(issuer);
        var survivor = NativeCleanupFlow.Field(await NativeCleanupFlow.ExchangeAsync(issuer, unrelated), OpenIddictConstants.Parameters.RefreshToken);
        Assert.Equal(200, (await NativeCleanupFlow.ExchangeAsync(issuer, code)).Status);
        var before = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(before);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, before.Status);
        Assert.Contains(before.Tokens, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.RefreshToken && row.Status == OpenIddictConstants.Statuses.Valid);
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        var after = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(after);
        Assert.Equal(before.Authorization, after.Authorization);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, after.Status);
        Assert.Equal(before.Tokens.Count, after.Tokens.Count);
        Assert.All(after.Tokens, row => Assert.Equal(OpenIddictConstants.Statuses.Revoked, row.Status));
        Assert.Equal(200, (await NativeCleanupFlow.RefreshAsync(issuer, survivor)).Status);
        var repeatedNonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, repeatedNonce), repeatedNonce));
        Assert.Equal(2, client.Bridge.Sends);
        Assert.Equal(2, client.Bridge.ProofDigests.Count);
    }

    /// <summary>Cleanup committed before code exchange denies a late native exchange without needing any issued token response.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_UnexchangedCode_When_CleanupCommitsFirst_Then_NativeLateExchangeIsDenied(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        var response = await NativeCleanupFlow.ExchangeAsync(issuer, code);
        Assert.Equal(400, response.Status);
        var state = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(state);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, state.Status);
        Assert.DoesNotContain(state.Tokens, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.RefreshToken);
    }

    /// <summary>A staged native response cannot escape an issuance rollback; a repeated intent cannot bind a second authorization.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_IssuanceRollbackOrDuplicateIntent_When_NativeCodeRuns_Then_SelectorCannotEscapeOrRebind(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var intent = NativeCleanupProfile.Random();
        var initial = await NativeCleanupFlow.CountsAsync(issuer);
        issuer.Rollback = true;
        var rolledBack = await Record.ExceptionAsync(() => NativeCleanupFlow.CodeAsync(issuer, intent));
        Assert.True(NativeCleanupFaults.CausedBy(rolledBack, issuer.Faults.Rollback));
        Assert.Equal(1, issuer.Faults.RollbackHits);
        issuer.Rollback = false;
        Assert.Null(await NativeCleanupFlow.StateAsync(issuer, intent));
        Assert.Equal(initial, await NativeCleanupFlow.CountsAsync(issuer));
        var code = await NativeCleanupFlow.CodeAsync(issuer, intent);
        var before = await NativeCleanupFlow.StateAsync(issuer, intent);
        Assert.NotNull(before);
        var inventory = await NativeCleanupFlow.CountsAsync(issuer);
        var duplicate = await Record.ExceptionAsync(() => NativeCleanupFlow.CodeAsync(issuer, intent));
        Assert.True(NativeCleanupFaults.CausedBy(duplicate, issuer.Faults.Duplicate));
        Assert.Equal(1, issuer.Faults.DuplicateHits);
        Assert.Equal(inventory, await NativeCleanupFlow.CountsAsync(issuer));
        var after = await NativeCleanupFlow.StateAsync(issuer, intent);
        Assert.NotNull(after);
        Assert.Equal(before.Authorization, after.Authorization);
        Assert.Equal(before.Tokens, after.Tokens);
        Assert.Equal(200, (await NativeCleanupFlow.ExchangeAsync(issuer, code)).Status);
    }

    /// <summary>Rollback suppresses confirmation and preserves usable native state; a fresh retry after the fault is removed succeeds.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_CleanupRollback_When_NativeClientWaits_Then_NoConfirmationEscapesAndRestoredRetryCommits(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        var refresh = NativeCleanupFlow.Field(await NativeCleanupFlow.ExchangeAsync(issuer, code), OpenIddictConstants.Parameters.RefreshToken);
        await using var client = new NativeCleanupClient(issuer);
        issuer.Rollback = true;
        var rolledBack = await Record.ExceptionAsync(() => client.RevokeAsync(code.Intent, NativeCleanupProfile.Random()));
        Assert.True(NativeCleanupFaults.CausedBy(rolledBack, issuer.Faults.Rollback));
        Assert.Equal(1, issuer.Faults.RollbackHits);
        Assert.True(issuer.Faults.StagedConfirmation);
        issuer.Rollback = false;
        var state = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(state);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, state.Status);
        Assert.Equal(200, (await NativeCleanupFlow.RefreshAsync(issuer, refresh)).Status);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        Assert.Equal(2, client.Bridge.Sends);
    }

    /// <summary>Native RFC7009 success and a replaced empty response do not satisfy the private nonce-bound confirmation contract.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_EmptyOrOrdinaryRevocationResponse_When_NativeClientReturns_Then_CleanupRemainsUnconfirmed(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        var ordinary = await client.RevokeAsync(code.Intent, nonce, OpenIddictConstants.TokenTypeHints.RefreshToken);
        Assert.False(NativeCleanupFlow.Confirmed(ordinary, nonce));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
        client.Bridge.EmptyResponse = true;
        Assert.False(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
        client.Bridge.EmptyResponse = false;
        var retryNonce = NativeCleanupProfile.Random();
        var confirmed = await client.RevokeAsync(code.Intent, retryNonce);
        Assert.True(NativeCleanupFlow.Confirmed(confirmed, retryNonce));
        Assert.False(NativeCleanupFlow.Confirmed(confirmed, nonce));
        Assert.Equal(3, client.Bridge.Sends);
    }

    /// <summary>Native discovery includes the same required cases for both migrated real providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;

    /// <summary>A genuinely issued refresh token still follows ordinary native target-only revocation, not the family-selector extension.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RealRefreshCredential_When_OrdinaryNativeRevocationRuns_Then_OnlyTargetIsRevokedWithoutPrivateConfirmation(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        var refresh = NativeCleanupFlow.Field(await NativeCleanupFlow.ExchangeAsync(issuer, code), OpenIddictConstants.Parameters.RefreshToken);
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        Assert.False(NativeCleanupFlow.Confirmed(await client.RevokeAsync(refresh, nonce, OpenIddictConstants.TokenTypeHints.RefreshToken), nonce));
        var state = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(state);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, state.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, Assert.Single(state.Tokens, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.RefreshToken).Status);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, Assert.Single(state.Tokens, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken).Status);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, Assert.Single(state.Tokens, row => row.Type == NativeCleanupProfile.SelectorType).Status);
        Assert.Equal(1, client.Bridge.Sends);
    }
}
