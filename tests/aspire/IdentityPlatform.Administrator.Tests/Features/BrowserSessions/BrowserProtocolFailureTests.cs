using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves final issuance suppresses native success when browser provenance, destinations or late source freshness change.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProtocolFailureTests(BootstrapFixture fixture)
{
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private const string ProviderTrait = "Provider";
    private const string ShortAge = "10";
    private const int PasswordOffset = 1;
    private const int MaximumAgeSeconds = 10;

    /// <summary>Malformed late projections cannot survive native signing and become published or committed credentials.</summary>
    [Theory, MemberData(nameof(Faults))]
    public async Task Given_ValidBrowserAuthority_When_PreparedBindingIsMutated_Then_FinalIssuanceRollsBack(string provider, int variant)
    {
        var probe = new BrowserProtocolFault();
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: probe.Configure, fullProtocol: true);
        var fault = (BrowserProtocolFault.Fault)variant;
        var authorization = fault is BrowserProtocolFault.Fault.ProjectedOrigin or BrowserProtocolFault.Fault.CodeOrigin;
        var artifact = authorization ? NativeAccountFlow.ReadReference(await browser.PushAsync()) : await BrowserProtocolFlow.CodeAsync(browser);
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        var binding = await browser.AttachmentSnapshotAsync();
        probe.Selected = fault;
        try
        {
            var response = authorization ? await BrowserProtocolFlow.AuthorizeAsync(browser, artifact) :
                await NativeAccountFlow.ExchangeAsync(browser.Scenario, artifact, browser.Services);
            RequireSuppressed(response);
            Assert.Equal(1, probe.Reached);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
            Assert.Equal(binding, await browser.AttachmentSnapshotAsync());
        }
        finally { probe.Clear(); }
    }

    /// <summary>The last source-authority check must occur after the new browser-row await, including a shorter requested age at equality.</summary>
    [Theory, MemberData(nameof(BrowserProtocolTests.Providers), MemberType = typeof(BrowserProtocolTests))]
    public async Task Given_ShortRequestedAge_When_FinalBrowserReadCrossesFreshnessDeadline_Then_NoTokenCommits(string provider)
    {
        var probe = new BrowserProtocolFault();
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: probe.Configure, fullProtocol: true);
        var reference = NativeAccountFlow.ReadReference(await browser.PushAsync(maximumAge: ShortAge));
        var code = NativeAccountFlow.ReadCode(browser.Scenario, await BrowserProtocolFlow.AuthorizeAsync(browser, reference));
        var before = await NativeAccountPersistence.CountAsync(browser.Scenario);
        var row = await browser.RowAsync();
        probe.FreshnessDeadline = DateTimeOffset.FromUnixTimeSeconds(row.CreatedAt + PasswordOffset + MaximumAgeSeconds);
        probe.Advance = () => browser.Journey.Clock.Now = probe.FreshnessDeadline.Value;
        probe.Selected = BrowserProtocolFault.Fault.FinalFreshness;
        try
        {
            RequireSuppressed(await NativeAccountFlow.ExchangeAsync(browser.Scenario, code, browser.Services));
            Assert.Equal(1, probe.Reached);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(browser.Scenario));
        }
        finally { probe.Clear(); }
    }

    /// <summary>Requires the exact final commit-denial shape without accepting arbitrary assertion or database failures as a pass.</summary>
    private static void RequireSuppressed(NativeAccountResult response)
    {
        NativeBrowserParFixture.RequireUnavailable(response);
        Assert.False(response.HasCookie);
        Assert.False(response.Claimed);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.Empty(response.Body);
        Assert.Empty(response.Location);
    }

    /// <summary>Discovers each private-code and ID/access destination mutation separately on both actual providers.</summary>
    public static IEnumerable<TheoryDataRow<string, int>> Faults => from provider in ProviderNames
                                                                    from fault in Enum.GetValues<BrowserProtocolFault.Fault>().Where(value => value != BrowserProtocolFault.Fault.FinalFreshness)
                                                                    select new TheoryDataRow<string, int>(provider, (int)fault).WithTrait(ProviderTrait, provider);
}
