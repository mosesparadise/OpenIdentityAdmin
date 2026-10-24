using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Connects actual native client state, issuer ceremony and callback across independent roots over real provider stores.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProtocolClientTests(BootstrapFixture fixture)
{
    /// <summary>The new ceremony and ID-only claims survive real native state, PAR, S256, nonce and callback validation exactly once.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_BoundNativeChallenge_When_IndependentClientHandlesCallback_Then_ExactFreshIdentityControlsSingleExchange(string provider, bool mismatch)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var ordinary = NativeClientInteroperabilityTests.Profile(keys);
        var profile = ordinary.ForBrowserSessions();
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true, policy: ordinary.Registration);
        var row = await browser.RowAsync();
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var first = new NativeClientHttp(database, browser.Scenario, profile, keys,
            services => Capture(services, profile, browser, row.ExpiresAt), protocolServices: browser.Services);
        await using var second = new NativeClientHttp(database, browser.Scenario, profile, keys, protocolServices: browser.Services);
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await AuthorizeAsync(browser, challenge.Location);
        second.Bridge.RewriteBrowserIntent = mismatch;
        var accepted = await second.InvokeAsync(callback, challenge.Cookie);
        if (mismatch)
        {
            NativeClientInteroperabilityTests.RequireClean(accepted);
            Assert.Equal(StatusCodes.Status400BadRequest, accepted.Status);
            Assert.False(accepted.Authentication?.Succeeded == true);
            Assert.Equal(1, second.Bridge.IdentityRewrites);
        }
        else
        {
            NativeClientInteroperabilityTests.RequireAccepted(accepted);
            BrowserProtocolFlow.RequireIdentity(accepted.Authentication!.Principal!, row);
            Assert.Equal(0, accepted.BodyLength);
        }
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
        NativeClientDenial.RequireReplay(await first.InvokeAsync(callback, challenge.Cookie));
        Assert.Equal(1, first.Bridge.ParSends + second.Bridge.ParSends);
        Assert.Equal(1, first.Bridge.TokenSends + second.Bridge.TokenSends);
    }
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] MismatchVariants = [false, true];
    private const string ProviderTrait = "Provider";
    /// <summary>Discovers genuine and independently re-signed wrong-intent responses on both stores.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Variants => from provider in ProviderNames
                                                                       from mismatch in MismatchVariants
                                                                       select new TheoryDataRow<string, bool>(provider, mismatch).WithTrait(ProviderTrait, provider);
    private const int CaptureOffset = 250;
    private const int BeforeParOffset = 750;

    /// <summary>A late custom-field change after durable state creation cannot send a different intent to the issuer.</summary>
    [Theory, MemberData(nameof(BrowserProtocolTests.Providers), MemberType = typeof(BrowserProtocolTests))]
    public async Task Given_CapturedNativeIntent_When_ParameterChangesBeforePar_Then_NoRemoteRequestIsSent(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var ordinary = NativeClientInteroperabilityTests.Profile(keys);
        var profile = ordinary.ForBrowserSessions();
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true, policy: ordinary.Registration);
        var row = await browser.RowAsync();
        await using var database = await fixture.LeaseSessionsAsync(provider);
        var reached = 0;
        await using var client = new NativeClientHttp(database, browser.Scenario, profile, keys, services =>
        {
            Capture(services, profile, browser, row.ExpiresAt);
            services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler
                .SetOrder(OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order - BeforeParOffset).UseInlineHandler(context =>
                {
                    context.Parameters[BrowserProtocolFlow.Intent] = browser.Binding.Completion;
                    reached++;
                    return ValueTask.CompletedTask;
                }));
        }, protocolServices: browser.Services);
        var denied = await client.InvokeAsync();
        Assert.IsType<InvalidOperationException>(denied.Failure);
        Assert.False(denied.Authentication?.Succeeded == true);
        Assert.Equal(1, reached);
        Assert.Equal(0, client.Bridge.ParSends);
        Assert.Equal(0, client.Bridge.TokenSends);
        await NativeClientInteroperabilityTests.RequireStatesAsync(client, total: 1, redeemed: 0);
    }

    /// <summary>Supplies the internal captured demand after native profile negotiation and before the unchanged challenge guard.</summary>
    private static void Capture(IServiceCollection services, NativeClientProfile profile, NativeBrowserParFixture browser, long deadline) =>
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler
            .SetOrder(OpenIddictClientHandlers.AttachResponseMode.Descriptor.Order + CaptureOffset).UseInlineHandler(context =>
            {
                NativeBrowserChallenge.Attach(context, profile, browser.Binding, DateTimeOffset.FromUnixTimeSeconds(deadline));
                return ValueTask.CompletedTask;
            }));

    /// <summary>Uses the genuine completed cookie and exact native challenge query without altering any credential or return binding.</summary>
    private static async Task<Uri> AuthorizeAsync(NativeBrowserParFixture browser, string location)
    {
        var request = new Uri(location);
        Assert.Equal(new Uri(browser.Scenario.Issuer, AdministratorProtocolScenario.AuthorizePath).AbsoluteUri, request.GetLeftPart(UriPartial.Path));
        var fields = QueryHelpers.ParseQuery(request.Query).ToDictionary(pair => pair.Key, pair => Assert.Single(pair.Value)!);
        var response = await NativeAccountHttp.InvokeAsync(browser.Scenario, AdministratorProtocolScenario.AuthorizePath, fields,
            services: browser.Services, configureHttp: http => http.Request.Headers.Cookie = browser.FreshCookie);
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status302Found);
        var callback = new Uri(response.Location);
        Assert.False(QueryHelpers.ParseQuery(callback.Query).ContainsKey(OpenIddictConstants.Parameters.Error));
        return callback;
    }
}
