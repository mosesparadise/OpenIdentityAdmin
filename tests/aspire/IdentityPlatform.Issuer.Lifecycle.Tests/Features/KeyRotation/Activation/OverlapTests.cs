using System.Net;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.WebUtilities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.KeyRotation.Activation;

[Collection("distribution")]
public sealed class OverlapTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Staging_mixed_activation_premature_removal_and_retained_rollback_preserve_exact_artifacts(string provider)
    {
        await using var fixture = await KeyRotationEnvironment.CreateAsync(provider);
        var host = fixture.Host;
        var oldSigning = fixture.Original.Signing.Thumbprint;
        var oldEncryption = fixture.Original.Encryption.Thumbprint;
        var newSigning = fixture.Next.Signing.Thumbprint;
        var newEncryption = fixture.Next.Encryption.Thumbprint;
        var stale = await KeyEvidence.PublicKeysAsync(host, "auth", oldSigning);
        var cookies = new CookieContainer();
        using var browser = host.NodeClient("auth", cookies, autoRedirect: true);
        var original = await host.AuthorizeAsync(browser);
        using var oldTokens = await DistributionEvidence.RedeemAsync(host, "auth", original);
        var pendingCookies = new CookieContainer();
        using var pendingBrowser = host.NodeClient("auth", pendingCookies, autoRedirect: true);
        var pending = await TestLogin.BeginAsync(host, pendingBrowser);
        var completedCookies = new CookieContainer();
        using var completedBrowser = host.NodeClient("auth", completedCookies, autoRedirect: true);
        var completed = await TestLogin.BeginAsync(host, completedBrowser);
        var continuation = await completed.CompleteAsync(host, completedBrowser);

        foreach (var node in DistributionEvidence.Nodes)
        {
            await fixture.ChangeAsync(node, KeyStage.Staged);
            await KeyEvidence.PublicKeysAsync(host, node, oldSigning, newSigning);
            await IssueAcrossAsync(fixture, node, DistributionEvidence.Other(node), oldSigning, oldEncryption);
        }
        var frozen = await KeyEvidence.PublicKeysAsync(host, "auth", oldSigning, newSigning);
        await fixture.ChangeAsync("auth-secondary", KeyStage.Active);
        await IssueAcrossAsync(fixture, "auth", "auth-secondary", oldSigning, oldEncryption);
        await IssueAcrossAsync(fixture, "auth-secondary", "auth", newSigning, newEncryption);
        using (var issuingBrowser = host.NodeClient("auth-secondary", autoRedirect: true))
        {
            var flow = await host.AuthorizeAsync(issuingBrowser);
            using var tokens = await DistributionEvidence.RedeemAsync(host, "auth-secondary", flow);
            await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", flow, tokens.RootElement, newSigning, frozen);
            await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", flow, tokens.RootElement, newSigning, stale, valid: false);
            await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", flow, tokens.RootElement, newSigning);
            var spliced = oldTokens.RootElement.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.Clone(), StringComparer.Ordinal);
            spliced["access_token"] = tokens.RootElement.GetProperty("access_token").Clone();
            var invalidPair = System.Text.Json.JsonSerializer.SerializeToElement(spliced);
            Assert.Equal("Access token hash mismatch.", await host.RelyingParty.ValidateAsync(invalidPair, original,
                TestContext.Current.CancellationToken, frozenJwks: frozen));
        }

        await ReversibleMissingEncryptionAsync(fixture, "auth", "auth-secondary", KeyStage.WithoutOldEncryption, KeyStage.Active,
            oldSigning, oldEncryption);
        await fixture.ChangeAsync("auth", KeyStage.Active);
        await KeyEvidence.PublicKeysAsync(host, "auth", oldSigning, newSigning);
        await IssueAcrossAsync(fixture, "auth", "auth-secondary", newSigning, newEncryption);
        await DistributionEvidence.SilentAsync(host, "auth-secondary", cookies, original);
        await CompleteOriginalFormsAsync(host, pending, pendingCookies, completed, completedCookies, continuation);

        await fixture.ChangeAsync("auth-secondary", KeyStage.WithoutOldSigning);
        var premature = await KeyEvidence.PublicKeysAsync(host, "auth-secondary", newSigning);
        await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", original, oldTokens.RootElement, oldSigning, premature, valid: false);
        await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", original, oldTokens.RootElement, oldSigning, frozen);
        await fixture.ChangeAsync("auth-secondary", KeyStage.Active);
        await KeyEvidence.RequirePublicAsync(fixture, "auth-secondary", original, oldTokens.RootElement, oldSigning);

        Assert.True(fixture.Retirement.ConfirmEnded(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow, ProcessTreeEvidence.Terminated));
        var priorBoundary = fixture.Retirement.Earliest;
        await fixture.ChangeAsync("auth", KeyStage.Rollback);
        Assert.Throws<InvalidOperationException>(() => fixture.Retirement.Earliest);
        await IssueAcrossAsync(fixture, "auth-secondary", "auth", newSigning, newEncryption);
        await IssueAcrossAsync(fixture, "auth", "auth-secondary", oldSigning, oldEncryption);
        await ReversibleMissingEncryptionAsync(fixture, "auth-secondary", "auth", KeyStage.OldOnly, KeyStage.Rollback,
            newSigning, newEncryption);
        await fixture.ChangeAsync("auth", KeyStage.Active);
        await IssueAcrossAsync(fixture, "auth", "auth-secondary", newSigning, newEncryption);
        await DistributionEvidence.SilentAsync(host, "auth", cookies, original);
        Assert.True(fixture.Retirement.ConfirmEnded(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow, ProcessTreeEvidence.Terminated));
        Assert.True(fixture.Retirement.Earliest > priorBoundary, "Rollback did not reset the observed retirement boundary.");
    }

    private static async Task<TestFlow> IssueAcrossAsync(KeyRotationEnvironment fixture, string source, string target, string signing, string encryption)
    {
        var host = fixture.Host;
        var request = await CachedRequestEvidence.CreateAsync(host, source);
        await KeyEvidence.RequirePayloadAsync(fixture, request.Reference, TokenTypeIdentifiers.Private.RequestToken, signing, encryption);
        var requestEntry = await KeyEvidence.ImmutableEntryAsync(host, request.Reference);
        await request.CompleteAsync(host, target);
        Assert.True(requestEntry == await KeyEvidence.ImmutableEntryAsync(host, request.Reference), "Accepted cached request changed immutable native fields.");
        using var browser = host.NodeClient(source, autoRedirect: true);
        var flow = await host.AuthorizeAsync(browser);
        await KeyEvidence.RequirePayloadAsync(fixture, flow.Code, TokenTypeIdentifiers.Private.AuthorizationCode, signing, encryption);
        var codeEntry = await KeyEvidence.ImmutableEntryAsync(host, flow.Code);
        using var tokens = await DistributionEvidence.RedeemAsync(host, target, flow);
        await KeyEvidence.RequirePublicAsync(fixture, target, flow, tokens.RootElement, fixture.ExpectedSigning(target));
        Assert.True(codeEntry == await KeyEvidence.ImmutableEntryAsync(host, flow.Code), "Accepted code changed immutable native fields.");
        return flow;
    }

    private static async Task ReversibleMissingEncryptionAsync(KeyRotationEnvironment fixture, string source, string target,
        KeyStage missing, KeyStage restore, string signing, string encryption)
    {
        var host = fixture.Host;
        var request = await CachedRequestEvidence.CreateAsync(host, source);
        var requestExpiry = await KeyEvidence.RequirePayloadAsync(fixture, request.Reference, TokenTypeIdentifiers.Private.RequestToken, signing, encryption);
        using var browser = host.NodeClient(source, autoRedirect: true);
        var code = await ExchangeEvidence.IssueAsync(host, browser);
        var codeExpiry = await KeyEvidence.RequirePayloadAsync(fixture, code.Flow.Code, TokenTypeIdentifiers.Private.AuthorizationCode, signing, encryption);
        await fixture.ChangeAsync(target, missing);
        using (var transport = host.NodeClient(target))
        {
            var before = await ExchangeSnapshot.CaptureAsync(host);
            using var denied = await host.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, transport: transport,
                cancellationToken: TestContext.Current.CancellationToken);
            await code.AssertDeniedAsync(host, denied, before);
            await DistributionEvidence.NativeErrorAsync(denied, "ID2004");
            await code.RequireFreshAsync(host);
        }
        await request.RequireDeniedAsync(host, target);
        await fixture.ChangeAsync(target, restore);
        Assert.Equal(codeExpiry, await KeyEvidence.RequirePayloadAsync(fixture, code.Flow.Code, TokenTypeIdentifiers.Private.AuthorizationCode, signing, encryption));
        using var tokens = await DistributionEvidence.RedeemAsync(host, target, code.Flow);
        Assert.Equal(requestExpiry, await KeyEvidence.RequirePayloadAsync(fixture, request.Reference, TokenTypeIdentifiers.Private.RequestToken, signing, encryption));
        await request.CompleteAsync(host, target);
    }

    private static async Task CompleteOriginalFormsAsync(IssuerEnvironment host, TestLogin pending, CookieContainer pendingCookies,
        TestLogin completed, CookieContainer completedCookies, Uri continuation)
    {
        using var owner = host.NodeClient("auth-secondary", pendingCookies, autoRedirect: true);
        using var foreign = host.NodeClient("auth-secondary", autoRedirect: true);
        var foreignForm = await TestLogin.BeginAsync(host, foreign);
        var before = await RestartEvidence.StateAsync(host);
        using (var denied = await pending.SubmitAsync(host, foreign, antiforgery: foreignForm.Antiforgery))
        {
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        using (var denied = await pending.SubmitAsync(host, owner, antiforgery: "invalid-proof"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(host, before);
        var next = await pending.CompleteAsync(host, owner);
        await pending.FollowAsync(host, owner, next);
        using var pendingTokens = await DistributionEvidence.RedeemAsync(host, "auth-secondary", pending.Flow);
        before = await RestartEvidence.StateAsync(host);
        using (var outsider = host.NodeClient("auth"))
        using (var denied = await outsider.GetAsync(continuation, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var target = denied.Headers.Location;
            Assert.True(target is not null && target.IsAbsoluteUri && target.Fragment.Length == 0 &&
                target.GetLeftPart(UriPartial.Path) == host.RelyingParty.Callback, "Foreign continuation used an unexpected callback.");
            var query = QueryHelpers.ParseQuery(target.Query);
            Assert.True(query["iss"] == host.Issuer.AbsoluteUri, "Foreign continuation changed the canonical issuer.");
            Assert.True(query["error"] == "access_denied" && query["state"] == completed.Flow.State);
            Assert.All(query.Keys, key => Assert.True(key is "error" or "error_description" or "error_uri" or "state" or "iss"));
            Assert.False(completed.Flow.Completion.Task.IsCompleted);
        }
        await RestartEvidence.RequireUnchangedAsync(host, before);
        var identity = await RestartEvidence.InteractionAsync(host, completed.Interaction);
        using var completedOwner = host.NodeClient("auth", completedCookies, autoRedirect: true);
        await completed.FollowAsync(host, completedOwner, continuation);
        using var completedTokens = await DistributionEvidence.RedeemAsync(host, "auth", completed.Flow);
        var consumed = await RestartEvidence.InteractionAsync(host, completed.Interaction);
        Assert.True(consumed.Consumed && consumed.CompletedSessionId == identity.CompletedSessionId && consumed.CompletedUserId == identity.CompletedUserId);
        before = await RestartEvidence.StateAsync(host);
        using var replay = await completedOwner.GetAsync(continuation, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(1, completed.Flow.CallbackCount);
        await RestartEvidence.RequireUnchangedAsync(host, before);
    }
}
