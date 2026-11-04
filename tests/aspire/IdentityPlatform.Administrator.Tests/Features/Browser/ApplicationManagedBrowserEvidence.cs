using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins actual React creation/editing to the same managed browser ceremony and independent provider/native cryptographic evidence.</summary>
internal static class ApplicationManagedBrowserEvidence
{
    private const int BackchannelSeconds = 12;
    private const int Unauthorized = 401;
    private const int ConsumedInputCount = 2;
    private const int IssuedTokenCount = 4;
    private const string AccessField = "access_token";
    private const string ApplicationsPath = "/applications";

    /// <summary>Creates no alternate registration or HTTP login; one native administrator browser owns the editor and subsequent managed ceremony.</summary>
    internal static async Task<string[]> RunAsync(BootstrapFixture fixture, string provider, bool approve)
    {
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true);
        await using var browser = await BrowserSession.CreateAsync();
        await using var relyingParty = new ManagedBrowserRelyingParty();
        await relyingParty.StartAsync(environment.Profile.Issuer);
        var page = await browser.Context.NewPageAsync();
        await ApplicationAccessFixture.SignInAsync(environment, page);
        await using var observation = await ApplicationLifecycleObservation.CreateAsync(page);
        var registration = await ApplicationEditorJourney.RunAsync(page, environment.Profile.ClientUri,
            environment.Issuer.Account.Journey.Clock.Now, WorkspacePresentation.RequireStateAsync, relyingParty.Callback);
        using var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(BackchannelSeconds)
        };
        var evidence = await BorrowAsync(environment, transport, registration);
        var authority = await ManagedHumanEvidence.AuthorityAsync(evidence);
        var flow = await ManagedBrowserJourney.RunAsync(page, relyingParty, registration,
            environment.Issuer.Account.Journey.Lease.Username, environment.Issuer.Account.Journey.Lease.Password,
            () => IssuerContinuationProviderFixture.UnusedCodeAsync(environment.Issuer.Account.Journey),
            environment.Issuer.Account.ActorId, approve, PresentationAsync);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(evidence));
        await VerifyAsync(evidence, flow, approve);
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, ApplicationsPath).AbsoluteUri);
        await BrowserSecretOracle.StorageAsync(page, flow.Secrets);
        await observation.RequireAsync(flow.Secrets);
        return flow.Secrets;
    }

    /// <summary>Binds independently persisted ordinal native identity to the actual create-response ID and visible client ID.</summary>
    private static async Task<ManagedTokenEvidenceContext> BorrowAsync(BrowserHttpsFixture environment, HttpClient transport, ApplicationEditorRegistration registration)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var application = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking().SingleAsync(row => row.Id == registration.Id, TestContext.Current.CancellationToken);
        Assert.True(application.Id == registration.Id && application.ClientId == registration.ClientId);
        return new(environment, transport, registration.Id, registration.ClientId, environment.Issuer.Account.CreateScope);
    }

    /// <summary>Requires exact native interaction, code/token and authority outcomes, then verifies genuine managed credentials cannot call the Admin API.</summary>
    private static async Task VerifyAsync(ManagedTokenEvidenceContext evidence, ManagedBrowserFlow flow, bool approve)
    {
        var snapshot = await evidence.SnapshotAsync(applicationOnly: true);
        var interaction = Assert.Single(snapshot.Interactions);
        Assert.True(interaction.ApplicationId == flow.Registration.Id && interaction.UserId == evidence.Environment.Issuer.Account.ActorId &&
            interaction.PasswordVerifiedAt is not null && interaction.MfaVerifiedAt is not null);
        if (!approve)
        {
            Assert.Equal(ManagedLoginState.Denied, interaction.State);
            var request = Assert.Single(snapshot.Tokens);
            Assert.True(request.Id == interaction.RequestId && request.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken &&
                request.Status == OpenIddictConstants.Statuses.Valid);
            Assert.Equal(0, snapshot.Authorizations);
            Assert.Null(interaction.CodeId);
            return;
        }
        Assert.Equal(ManagedLoginState.Consumed, interaction.State);
        Assert.NotNull(interaction.ConsentVerifiedAt);
        Assert.Equal(IssuedTokenCount, snapshot.Tokens.Length);
        Assert.Equal(1, snapshot.Authorizations);
        var consumed = snapshot.Tokens.Where(row => row.Id == interaction.RequestId || row.Id == interaction.CodeId).ToArray();
        Assert.Equal(ConsumedInputCount, consumed.Length);
        Assert.All(consumed, row => Assert.True(row.Status == OpenIddictConstants.Statuses.Redeemed));
        await ManagedTokenOracle.ValidateAsync(evidence, flow.Nonce, interaction.RequestId!, flow.Body, interaction.UserId!, applicationOnly: true);
        await ManagedTokenEvidence.IssuanceAuditAsync(evidence, applicationOnly: true, requestId: interaction.RequestId);
        await ApplicationAccessFixture.RequireAsync(evidence.Environment.Api!.Address, flow.Body.GetProperty(AccessField).GetString(), Unauthorized);
    }

    /// <summary>Uses existing native Razor theme, keyboard/reflow and sanitized axe checks on password, MFA, consent and cancellation pages.</summary>
    private static async Task PresentationAsync(IPage page)
    {
        await BrowserLoginPresentation.PageAsync(page, dark: false);
        await BrowserLoginPresentation.PageAsync(page, dark: true);
    }
}
