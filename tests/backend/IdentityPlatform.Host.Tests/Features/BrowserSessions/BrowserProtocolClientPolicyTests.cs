using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Proves bound challenge/state policy and final callback claim correspondence through installed native preparation and cryptography.</summary>
public sealed class BrowserProtocolClientPolicyTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const int RemainingSeconds = 20;
    private const string Missing = "missing";
    private const string Duplicate = "duplicate";
    private const string Replaced = "replaced";
    private const string Extended = "extended";
    private const string Selector = "selector";

    /// <summary>Only an explicit captured attempt adds selectors to PAR and its digest to actual protected state; later representation changes deny.</summary>
    [Theory]
    [InlineData(Missing)]
    [InlineData(Duplicate)]
    [InlineData(Replaced)]
    [InlineData(Extended)]
    [InlineData(Selector)]
    public async Task Given_CapturedBrowserDemand_When_StateOrTransportChanges_Then_OriginalNativeBindingDenies(string fault)
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var context = BrowserLoginClientTransportTests.Context(services);
        context.Principal!.SetClaim(BrowserSessionIntent.ClaimType, (string?)null);
        var profile = services.GetRequiredService<NativeClientProfile>().ForBrowserSessions();
        var deadline = context.Options.TimeProvider!.GetUtcNow().AddSeconds(RemainingSeconds);
        NativeBrowserChallenge.Attach(context, profile, Binding(), deadline);
        NativeClientChallenge.Require(context, profile);
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(context);
        await new NativeBrowserState().HandleAsync(context);
        NativeClientChallenge.RequireState(context, profile);
        Assert.True(context.StateTokenPrincipal!.GetExpirationDate() <= deadline);
        var encoded = await NativeClientRetainedKeyTests.GenerateAsync(services, keys, context.StateTokenPrincipal);
        var restored = await NativeClientRetainedKeyTests.ValidateAsync(context.Options, encoded);
        Assert.Equal(BrowserLoginBindingTests.Digest, BrowserSessionIntent.Read(restored));
        Assert.DoesNotContain(restored.Claims, claim => claim.Value == BrowserLoginBindingTests.Completion || claim.Value == BrowserLoginBindingTests.Intent);
        Change(context, fault, deadline);
        Assert.Throws<InvalidOperationException>(() => NativeClientChallenge.RequireState(context, profile));
    }

    /// <summary>Ordinary profile and invalid original deadlines cannot authorize transport merely by possessing canonical selectors.</summary>
    [Theory]
    [InlineData(false, RemainingSeconds)]
    [InlineData(true, 0)]
    [InlineData(true, 301)]
    public void Given_UncapturedDemand_When_ProfileOrDeadlineIsInvalid_Then_AttachmentDenies(bool browser, int seconds)
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        using var services = NativeClientCompositionTests.Create(keys);
        var context = BrowserLoginClientTransportTests.Context(services);
        var profile = services.GetRequiredService<NativeClientProfile>();
        if (browser)
        {
            profile = profile.ForBrowserSessions();
        }
        Assert.Throws<InvalidOperationException>(() => NativeBrowserChallenge.Attach(context, profile, Binding(), context.Options.TimeProvider!.GetUtcNow().AddSeconds(seconds)));
    }

    /// <summary>A caller-supplied private expectation cannot become an ordinary B2 protected-state field.</summary>
    [Fact]
    public async Task Given_OrdinaryChallenge_When_UnrequestedBrowserExpectationIsPrepared_Then_DurableStateGuardDenies()
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var context = BrowserLoginClientTransportTests.Context(services);
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(context);
        var profile = services.GetRequiredService<NativeClientProfile>();
        Assert.Throws<InvalidOperationException>(() => NativeClientChallenge.RequireState(context, profile));
        context.StateTokenPrincipal!.SetClaim(BrowserSessionIntent.ClaimType, (string?)null);
        NativeClientChallenge.RequireState(context, profile);
    }

    /// <summary>Final B3 validation requires signed claims and the exact protected expectation together, after native crypto has accepted the fixture token.</summary>
    [Theory]
    [InlineData(Missing)]
    [InlineData(Duplicate)]
    [InlineData(Replaced)]
    [InlineData(Selector)]
    public async Task Given_NativeValidatedIdentity_When_ProtectedExpectationOrClaimsAreChanged_Then_FinalBrowserGuardDenies(string fault)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        var state = new ClaimsPrincipal(new ClaimsIdentity([new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent)]));
        var context = new OpenIddictClientEvents.ProcessAuthenticationContext(new OpenIddictClientTransaction
        {
            Options = new() { TimeProvider = TimeProvider.System },
            Logger = NullLogger.Instance
        })
        {
            StateTokenPrincipal = state,
            BackchannelIdentityTokenPrincipal = token.Principal,
            BackchannelIdentityToken = token.Encoded
        };
        Assert.True(NativeBrowserIdentity.Matches(context));
        if (fault == Missing)
        {
            state.SetClaim(BrowserSessionIntent.ClaimType, (string?)null);
        }
        if (fault == Duplicate)
        {
            ((ClaimsIdentity)state.Identity!).AddClaim(new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent));
        }
        if (fault == Replaced)
        {
            state.SetClaim(BrowserSessionIntent.ClaimType, BrowserLoginBindingTests.Digest);
        }
        if (fault == Selector)
        {
            token.Principal.SetClaim(BrowserSessionToken.SourceExpiryClaim, (string?)null);
        }
        await new NativeBrowserIdentity().HandleAsync(context);
        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
    }

    /// <summary>Creates canonical representation independently of durable ceremony authority.</summary>
    private static BrowserLoginBinding Binding()
    {
        var request = new OpenIddictRequest();
        request.SetParameter(BrowserLoginBinding.IntentParameter, BrowserLoginBindingTests.Intent);
        request.SetParameter(BrowserLoginBinding.CompletionParameter, BrowserLoginBindingTests.Completion);
        Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(request, out var binding));
        return binding!;
    }

    /// <summary>Changes one otherwise valid prepared binding without introducing unrelated profile or cryptographic failures.</summary>
    private static void Change(OpenIddictClientEvents.ProcessChallengeContext context, string fault, DateTimeOffset deadline)
    {
        var state = context.StateTokenPrincipal!;
        if (fault == Missing)
        {
            state.SetClaim(BrowserSessionIntent.ClaimType, (string?)null);
        }
        if (fault == Duplicate)
        {
            ((ClaimsIdentity)state.Identity!).AddClaim(new(BrowserSessionIntent.ClaimType, BrowserLoginBindingTests.Digest));
        }
        if (fault == Replaced)
        {
            context.Parameters[BrowserLoginBinding.IntentParameter] = BrowserLoginBindingTests.Other;
        }
        if (fault == Extended)
        {
            state.SetExpirationDate(deadline.AddSeconds(1));
        }
        if (fault == Selector)
        {
            Binding().CopyTo(state);
        }
    }
}
