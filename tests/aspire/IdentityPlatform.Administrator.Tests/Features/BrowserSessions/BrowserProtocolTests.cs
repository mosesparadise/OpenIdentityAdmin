using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises the opt-in issuer flow over real providers while preserving ordinary B2 and one-use native ownership.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProtocolTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string IntentField = "admin_login_intent";
    private const string CompletionField = "admin_login_completion";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] ExchangeVariants = [false, true];
    /// <summary>A genuine new ceremony supplies exact ID-only source claims; the old B2 request remains unbound under the same browser registration.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_CompletedCeremony_When_NativeCodeIsExchanged_Then_IdOnlyClaimsMatchOriginalSource(string provider)
    {
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true);
        var code = await BrowserProtocolFlow.CodeAsync(browser);
        await BrowserProtocolFlow.RequireTokensAsync(browser, await NativeAccountFlow.ExchangeAsync(browser.Scenario, code, browser.Services));
        NativeAccountDenialTests.RequireJson(await NativeAccountFlow.ExchangeAsync(browser.Scenario, code, browser.Services), OpenIddictConstants.Errors.InvalidGrant);
        var ordinary = await NativeAccountFlow.PushAsync(browser.Scenario, browser.Services);
        var ordinaryCode = NativeAccountFlow.ReadCode(browser.Scenario, await BrowserProtocolFlow.AuthorizeAsync(browser, ordinary, fresh: false));
        await BrowserProtocolFlow.RequireTokensAsync(browser, await NativeAccountFlow.ExchangeAsync(browser.Scenario, ordinaryCode, browser.Services), bound: false);
    }

    /// <summary>A still-fresh previous session for the same subject cannot borrow a different completed ceremony.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_CompletedNewSession_When_OldFreshCookieAuthorizes_Then_NoCodeIsCreated(string provider)
    {
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true);
        var reference = NativeAccountFlow.ReadReference(await browser.PushAsync());
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        NativeAccountDenialTests.RequireRedirect(browser.Scenario,
            await BrowserProtocolFlow.AuthorizeAsync(browser, reference, fresh: false), OpenIddictConstants.Errors.InvalidGrant);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(browser.Scenario, (await browser.RowAsync()).RequestId!));
    }

    /// <summary>The completed selector cannot authenticate a code if its durable originating native request has changed.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_AttachedCompletion_When_OriginatingRequestIsSubstituted_Then_AuthorizationDenies(string provider)
    {
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true);
        var reference = NativeAccountFlow.ReadReference(await browser.PushAsync());
        await using (var scope = browser.Journey.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<IssuerContinuation>().SingleAsync(TestContext.Current.CancellationToken);
            row.RequestId = Guid.NewGuid().ToString();
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        NativeAccountDenialTests.RequireRedirect(browser.Scenario,
            await BrowserProtocolFlow.AuthorizeAsync(browser, reference), OpenIddictConstants.Errors.InvalidGrant);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
    }

    /// <summary>Token-body selectors cannot replace protected code provenance, including a canonical equal pair.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_BoundCode_When_BrowserFieldsAreInjectedAtExchange_Then_NoTokenIsCreated(string provider)
    {
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true);
        var code = await BrowserProtocolFlow.CodeAsync(browser);
        var fields = NativeAccountFlow.ExchangeFields(browser.Scenario, code);
        fields.Add(IntentField, browser.Binding.Intent);
        fields.Add(CompletionField, browser.Binding.Completion);
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        NativeAccountDenialTests.RequireJson(await NativeAccountHttp.InvokeAsync(browser.Scenario, AdministratorProtocolScenario.TokenPath,
            fields, services: browser.Services), OpenIddictConstants.Errors.InvalidRequest);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
    }

    /// <summary>Both current stores must retain the original intent deadline even when the source session is still live.</summary>
    [Theory, MemberData(nameof(Endpoints))]
    public async Task Given_OriginalIntentDeadline_When_AuthorizationOrExchangeReachesEquality_Then_NoCredentialIsIssued(string provider, bool exchange)
    {
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true);
        var row = await browser.RowAsync();
        browser.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt - 1);
        var reference = exchange ? await BrowserProtocolFlow.CodeAsync(browser) : NativeAccountFlow.ReadReference(await browser.PushAsync());
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        browser.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt);
        var response = exchange ? await NativeAccountFlow.ExchangeAsync(browser.Scenario, reference, browser.Services) :
            await BrowserProtocolFlow.AuthorizeAsync(browser, reference);
        if (exchange)
        {
            NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidGrant);
        }
        else
        {
            NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidToken);
        }
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
    }

    /// <summary>Supplies exact portable provider traits to native discovery and hosted evidence verification.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => ProviderNames
        .Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));

    /// <summary>Discovers authorization and exchange deadline boundaries independently on both actual databases.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Endpoints => from provider in ProviderNames
                                                                        from exchange in ExchangeVariants
                                                                        select new TheoryDataRow<string, bool>(provider, exchange).WithTrait(ProviderTrait, provider);
}
