using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires fixed-origin native CSRF forms before any product logout mutation or cookie publication.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutFramingTests(BootstrapFixture fixture)
{
    private const string OriginFault = "origin";
    private const string CsrfFault = "csrf";
    private const string ExtraFault = "extra";
    private const string DuplicateFault = "duplicate";
    private const string InvalidToken = "invalid";
    private const string ReturnField = "ReturnUrl";
    private const string ForeignReturn = "https://other.example.test/";
    private const string ForeignOrigin = "https://other.example.test";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] FormFaults = [OriginFault, CsrfFault, ExtraFault, DuplicateFault];
    private const string ProviderTrait = "Provider";
    private const string Origin = "Origin";

    /// <summary>Exact-origin/native-CSRF/form constraints reject before any durable mutation or native transport.</summary>
    [Theory, MemberData(nameof(Forms))]
    public async Task Given_ProtectedPendingGeneration_When_LogoutFormIsHostile_Then_StateAndCookiesRemainUnchanged(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var before = await scenario.SnapshotAsync();
        var fields = new List<KeyValuePair<string, string>> { new(AdministratorJourney.Antiforgery, fault == CsrfFault ? InvalidToken : token) };
        if (fault == ExtraFault)
        {
            fields.Add(new(ReturnField, ForeignReturn));
        }
        if (fault == DuplicateFault)
        {
            fields.Add(new(AdministratorJourney.Antiforgery, token));
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, BrowserLogoutHttp.Path) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add(Origin, fault == OriginFault ? ForeignOrigin : scenario.Profile.ClientUri.GetLeftPart(UriPartial.Authority));
        using var response = await scenario.Browser.SendAsync(request, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        using var positive = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
        await BrowserLogoutHttp.CommittedAsync(positive);
    }

    /// <summary>Discovers each independent protected mutation fault on both databases.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Forms => from provider in ProviderNames
                                                                      from fault in FormFaults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
