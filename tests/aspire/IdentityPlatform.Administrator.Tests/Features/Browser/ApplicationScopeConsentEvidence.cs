using System.Net.Http.Headers;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Continues the same UI-created scope and assigned application through genuine retained-engine consent, tokens and UserInfo.</summary>
internal static class ApplicationScopeConsentEvidence
{
    private const string Requested = "browser.orders.read openid";
    private const string Resource = "browser-orders-api";
    private const string ConsentHeading = "Review access";
    private const string ScopeList = "#requested-scopes";
    private const string ScopeName = "browser.orders.read";
    private const string Description = "Read your order history";
    private const string AccessField = "access_token";
    private const string Bearer = "Bearer";
    private const int BackchannelSeconds = 12;

    /// <summary>Approves one real extended flow and cancels another, preserving native cryptographic and durable no-extra-issuance controls.</summary>
    internal static async Task RunAsync(BrowserHttpsFixture environment, IPage page, ManagedBrowserRelyingParty relyingParty, ApplicationEditorRegistration registration)
    {
        using var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(BackchannelSeconds)
        };
        var evidence = new ManagedTokenEvidenceContext(environment, transport, registration.Id, registration.ClientId, environment.Issuer.Account.CreateScope);
        var projection = new ManagedTokenProjection(Requested, Resource);
        var flow = await JourneyAsync(environment, page, relyingParty, registration, projection, approve: true);
        var snapshot = await evidence.SnapshotAsync(applicationOnly: true);
        var row = Assert.Single(snapshot.Interactions);
        await ManagedTokenOracle.ValidateAsync(evidence, flow.Nonce, row.RequestId!, flow.Body, row.UserId!, applicationOnly: true, projection: projection);
        await ManagedTokenEvidence.IssuanceAuditAsync(evidence, applicationOnly: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(environment.Profile.Issuer, ManagedConsentTest.UserInfo));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, flow.Body.GetProperty(AccessField).GetString());
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        await ManagedConsentTest.RequireAsync(response, row.UserId!, null, false);
        var cancelled = await JourneyAsync(environment, page, relyingParty, registration, projection, approve: false);
        var after = await evidence.SnapshotAsync(applicationOnly: true);
        Assert.Equal(snapshot.Tokens.Length + 1, after.Tokens.Length);
        Assert.Equal(snapshot.Authorizations, after.Authorizations);
        Assert.Single(after.Interactions, value => value.State == ManagedLoginState.Denied);
        await BrowserSecretOracle.StorageAsync(page, [.. flow.Secrets, .. cancelled.Secrets]);
    }

    /// <summary>Uses the current UI registration and shared real account ceremony without replacing issuer forms or relying-party checks.</summary>
    private static Task<ManagedBrowserFlow> JourneyAsync(BrowserHttpsFixture environment, IPage page, ManagedBrowserRelyingParty relyingParty,
        ApplicationEditorRegistration registration, ManagedTokenProjection projection, bool approve) =>
        ManagedBrowserJourney.RunAsync(page, relyingParty, registration, environment.Issuer.Account.Journey.Lease.Username,
            environment.Issuer.Account.Journey.Lease.Password, () => IssuerContinuationProviderFixture.UnusedCodeAsync(environment.Issuer.Account.Journey),
            environment.Issuer.Account.ActorId, approve, PresentationAsync, projection);

    /// <summary>Checks visible scope/resource/disclosure and both themes with existing keyboard/reflow and sanitized axe assertions.</summary>
    private static async Task PresentationAsync(IPage page)
    {
        if (await page.GetByRole(AriaRole.Heading, new()
        {
            Name = ConsentHeading,
            Exact = true
        }).CountAsync() != 0)
        {
            await Assertions.Expect(page.Locator(ScopeList)).ToContainTextAsync(ScopeName);
            await Assertions.Expect(page.Locator(ScopeList)).ToContainTextAsync(Resource);
            await Assertions.Expect(page.Locator(ScopeList)).ToContainTextAsync(Description);
        }
        await BrowserLoginPresentation.PageAsync(page, dark: false);
        await BrowserLoginPresentation.PageAsync(page, dark: true);
    }
}
