using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves native endpoint handoff is necessary but cannot substitute for committed, formatted issuance.</summary>
public sealed class AdministratorPassthroughTests
{
    private const string UnexpectedLocation = "https://untrusted.example.test/";
    private const string NoStore = "no-store";
    private const string ContentPolicy = "default-src 'none'";
    private const string ChangedCachePolicy = "public";
    private const string ChangedState = "mutated";
    /// <summary>Both real native passthrough handlers bind only the same later handled sign-in, without granting publication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ActualNativePassthrough_When_SameSignInCompletes_Then_HandoffMatchesButDoesNotAuthorizeOutput(bool authorization)
    {
        await using var probe = new AdministratorPassthroughProbe(authorization);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            await current.HandoffAsync();
            var receipt = AdministratorPassthrough.Claim(current.SignIn);
            Assert.False(receipt.Allows(current.SignIn));
            current.SignIn.HandleRequest();
            Assert.True(receipt.Allows(current.SignIn));
            Assert.False(receipt.Allows(new(current.SignIn.Transaction)));
            AdministratorIssuanceCoordinator.ClaimEndpointPublication(current.Http);
        }));
    }

    /// <summary>Setting skipped state manually cannot produce the private observation required by either endpoint.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ArbitrarySkippedRequest_When_SignInClaimsHandoff_Then_RequestFailsClosed(bool authorization)
    {
        await using var probe = new AdministratorPassthroughProbe(authorization);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            await current.HandoffAsync(native: false);
            AdministratorPassthrough.Claim(current.SignIn);
        }));
    }

    /// <summary>The same receipt cannot be claimed twice, even with the identical native sign-in object.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ClaimedHandoff_When_Reused_Then_RequestFailsClosed(bool authorization)
    {
        await using var probe = new AdministratorPassthroughProbe(authorization);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            await current.HandoffAsync();
            AdministratorPassthrough.Claim(current.SignIn);
            AdministratorPassthrough.Claim(current.SignIn);
        }));
    }

    /// <summary>Mutation of either terminal context or native input after a positive observation invalidates that handoff.</summary>
    [Theory]
    [InlineData(Change.InnerRejected)]
    [InlineData(Change.OuterRejected)]
    [InlineData(Change.SignInRejected)]
    [InlineData(Change.SignInSkipped)]
    [InlineData(Change.RequestReplaced)]
    [InlineData(Change.Parameters)]
    [InlineData(Change.HttpReplaced)]
    public async Task Given_ObservedHandoff_When_NativeBindingChanges_Then_PreviousPositiveReceiptCannotAuthorize(Change change)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        await probe.RunAsync(async current =>
        {
            await current.HandoffAsync();
            var receipt = AdministratorPassthrough.Claim(current.SignIn);
            current.SignIn.HandleRequest();
            Assert.True(receipt.Allows(current.SignIn));
            Mutate(current, change);
            Assert.False(receipt.Allows(current.SignIn));
        });
    }

    /// <summary>A native skip must occur before staging response output and before the outer dispatch returns.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_StartedOrEarlyHandoff_When_ReceiptClaimed_Then_RequestFailsClosed(bool early)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            if (early)
            {
                await AdministratorPassthrough.TrackAsync(current.Outer, async () =>
                {
                    await current.NativeAsync();
                    AdministratorPassthrough.Claim(current.SignIn);
                });
            }
            else
            {
                await AdministratorPassthrough.TrackAsync(current.Outer, async () =>
                {
                    current.Http.Response.Headers.Location = UnexpectedLocation;
                    await current.NativeAsync();
                });
            }
        }));
    }

    /// <summary>Existing host protection headers survive a valid handoff; adding or changing one after entry is denied.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OriginalSecurityHeaders_When_NativeHandoffReturns_Then_OnlyUnchangedBaselineQualifies(bool mutate)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        var operation = probe.RunAsync(async current =>
        {
            current.Http.Response.Headers.CacheControl = NoStore;
            current.Http.Response.Headers.ContentSecurityPolicy = ContentPolicy;
            await AdministratorPassthrough.TrackAsync(current.Outer, async () =>
            {
                if (mutate)
                {
                    current.Http.Response.Headers.CacheControl = ChangedCachePolicy;
                }
                await current.NativeAsync();
                current.Outer.SkipRequest();
            });
            var receipt = AdministratorPassthrough.Claim(current.SignIn);
            current.SignIn.HandleRequest();
            Assert.True(receipt.Allows(current.SignIn));
            Assert.Equal("no-store", current.Http.Response.Headers.CacheControl.ToString());
        });
        if (mutate)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        }
        else
        {
            await operation;
        }
    }

    /// <summary>The same original request cannot authorize a substituted capture or another owned child scope.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ObservedHandoff_When_ParentCaptureOrScopeChanges_Then_ClaimFailsClosed(bool child)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            await current.HandoffAsync();
            if (child)
            {
                await ProtocolStorageChild.RunAsync(current.Factory, (_, _) =>
                    ValueTask.FromResult(AdministratorPassthrough.Claim(current.SignIn)));
            }
            else
            {
                using var replacement = new ProtocolResponseCapture(ProtocolStorageAmbient.Current!.Request);
                current.Http.Features.Set<IHttpResponseFeature>(replacement);
                current.Http.Features.Set<IHttpResponseBodyFeature>(replacement);
                AdministratorPassthrough.Claim(current.SignIn);
            }
        }));
    }

    /// <summary>Changes one bound value after the positive control without replacing any native handler.</summary>
    private static void Mutate(AdministratorPassthroughProbe probe, Change change)
    {
        switch (change)
        {
            case Change.InnerRejected:
                ((OpenIddictServerEvents.HandleAuthorizationRequestContext)probe.Inner).Reject();
                break;
            case Change.OuterRejected:
                probe.Outer.Reject();
                break;
            case Change.SignInRejected:
                probe.SignIn.Reject();
                break;
            case Change.SignInSkipped:
                probe.SignIn.SkipRequest();
                break;
            case Change.RequestReplaced:
                probe.Outer.Transaction.Request = new();
                break;
            case Change.Parameters:
                probe.Outer.Transaction.Request!.State = ChangedState;
                break;
            case Change.HttpReplaced:
                probe.Outer.Transaction.Properties.Remove(typeof(Microsoft.AspNetCore.Http.HttpRequest).FullName!);
                break;
        }
    }

    public enum Change
    {
        InnerRejected, OuterRejected, SignInRejected, SignInSkipped, RequestReplaced, Parameters, HttpReplaced
    }
}
