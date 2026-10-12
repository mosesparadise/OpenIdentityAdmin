using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Accounts.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RecoveryIndex = IdentityPlatform.AuthServer.Features.Accounts.Recovery.IndexModel;
using RecoveryVerify = IdentityPlatform.AuthServer.Features.Accounts.Recovery.VerifyModel;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Verifies fixed page dispatch and denial presentation without claiming native authentication acceptance.</summary>
public sealed class SecurityPageTests
{
    private const string Local = "/synthetic-local-continuation";
    private const string Login = "/account/administrator/login";
    private const string Query = "?extra=value";
    private const string Locator = "?Interaction=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Code = "synthetic-response-code";

    /// <summary>Every password entry page fixes its own action and renders generic denial before redirecting on success.</summary>
    [Theory]
    [InlineData(AdministratorSecurityAction.Recovery)]
    [InlineData(AdministratorSecurityAction.Replacement)]
    [InlineData(AdministratorSecurityAction.Regeneration)]
    public async Task Given_PasswordEntryPage_When_RequestIsHandled_Then_OnlyItsFixedActionIsDispatched(AdministratorSecurityAction action)
    {
        var spy = new JourneySpy();
        SecurityPasswordPage page = action switch
        {
            AdministratorSecurityAction.Recovery => new RecoveryIndex(spy),
            AdministratorSecurityAction.Replacement => new ReplacementModel(spy),
            _ => new RegenerationModel(spy)
        };
        Attach(page);
        Assert.IsType<PageResult>(page.OnGet());
        page.Request.QueryString = new QueryString(Query);
        Assert.IsType<BadRequestResult>(page.OnGet());
        page.Request.QueryString = QueryString.Empty;
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Equal(action, spy.Action);
        Assert.Equal(400, page.Response.StatusCode);
        Assert.NotNull(page.Error);
        spy.Location = Local;
        Assert.Equal(Local, Assert.IsType<LocalRedirectResult>(await page.OnPostAsync()).Url);
    }

    /// <summary>Step-up forms select recovery versus current-factor proof in code and never authorize on GET.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_StepUpPage_When_FormIsHandled_Then_FixedProofTypeAndGenericDenialArePreserved(bool recovery)
    {
        var spy = new JourneySpy();
        SecurityStepUpPage page = recovery ? new RecoveryVerify(spy) : new ReplaceModel(spy);
        Attach(page);
        Assert.IsType<BadRequestResult>(page.OnGet());
        page.Request.QueryString = new QueryString(Locator);
        Assert.IsType<PageResult>(page.OnGet());
        Assert.Null(spy.Recovery);
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Equal(recovery, spy.Recovery);
        Assert.Equal(400, page.Response.StatusCode);
        Assert.NotNull(page.Error);
        spy.Location = Local;
        Assert.Equal(Local, Assert.IsType<LocalRedirectResult>(await page.OnPostAsync()).Url);
    }

    /// <summary>Confirmation distinguishes withheld staged material and denial from an explicitly returned completion result.</summary>
    [Fact]
    public async Task Given_ConfirmationPage_When_JourneyDeniesThenCompletes_Then_OnlyReturnedMaterialIsRendered()
    {
        var spy = new JourneySpy();
        var page = new ConfirmModel(spy);
        Attach(page);
        Assert.IsType<BadRequestResult>(await page.OnGetAsync());
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.False(spy.Regeneration);
        Assert.Equal(400, page.Response.StatusCode);
        Assert.Null(page.RecoveryCodes);
        Assert.NotNull(page.Error);
        spy.Codes = [Code];
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Same(spy.Codes, page.RecoveryCodes);
    }

    /// <summary>Regeneration never returns a code collection on denial and always dispatches its fixed completion type.</summary>
    [Fact]
    public async Task Given_RegenerationPage_When_JourneyDeniesThenCompletes_Then_OnlyReturnedCodesAreRendered()
    {
        var spy = new JourneySpy();
        var page = new RegenerateModel(spy);
        Attach(page);
        Assert.IsType<BadRequestResult>(page.OnGet());
        page.Request.QueryString = new QueryString(Locator);
        Assert.IsType<PageResult>(page.OnGet());
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.True(spy.Regeneration);
        Assert.Equal(400, page.Response.StatusCode);
        Assert.Null(page.RecoveryCodes);
        Assert.NotNull(page.Error);
        spy.Codes = [Code];
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Same(spy.Codes, page.RecoveryCodes);
    }

    /// <summary>GET is inert and logout success redirects only to the fixed administrator login.</summary>
    [Fact]
    public async Task Given_LogoutPage_When_GetAndPostsRun_Then_OnlySuccessfulPostRedirectsToLogin()
    {
        var spy = new JourneySpy();
        var page = new LogoutModel(spy);
        Attach(page);
        Assert.IsType<PageResult>(page.OnGet());
        page.Request.QueryString = new QueryString(Query);
        Assert.IsType<BadRequestResult>(page.OnGet());
        Assert.False(spy.LogoutCalled);
        Assert.IsType<BadRequestResult>(await page.OnPostAsync());
        Assert.True(spy.LogoutCalled);
        spy.Logout = true;
        Assert.Equal(Login, Assert.IsType<LocalRedirectResult>(await page.OnPostAsync()).Url);
    }

    /// <summary>Provides only native page context; it does not install a cookie or authentication handler.</summary>
    private static void Attach(PageModel page) => page.PageContext = new PageContext { HttpContext = new DefaultHttpContext() };

    /// <summary>Records page dispatch only; no result represents native authorization or database acceptance.</summary>
    private sealed class JourneySpy : IAdministratorSecurityJourney
    {
        internal AdministratorSecurityAction? Action
        {
            get; private set;
        }
        internal bool? Recovery
        {
            get; private set;
        }
        internal bool? Regeneration
        {
            get; private set;
        }
        internal string? Location
        {
            get; set;
        }
        internal IReadOnlyList<string>? Codes
        {
            get; set;
        }
        internal bool Logout
        {
            get; set;
        }
        internal bool LogoutCalled
        {
            get; private set;
        }
        /// <summary>Records the fixed entry action.</summary>
        public Task<string?> BeginAsync(HttpContext context, AdministratorSecurityAction action)
        {
            Action = action;
            return Task.FromResult(Location);
        }
        /// <summary>Records the fixed proof type.</summary>
        public Task<string?> AuthorizeAsync(HttpContext context, bool recovery)
        {
            Recovery = recovery;
            return Task.FromResult(Location);
        }
        /// <summary>Records the fixed completion type and supplies only a presentation sentinel.</summary>
        public Task<IReadOnlyList<string>?> CompleteAsync(HttpContext context, bool regeneration)
        {
            Regeneration = regeneration;
            return Task.FromResult(Codes);
        }
        /// <summary>Models withheld stage material without constructing protected secrets.</summary>
        public Task<string?> StagedAsync(HttpContext context) => Task.FromResult<string?>(null);
        /// <summary>Records whether the page attempted a logout mutation.</summary>
        public Task<bool> LogoutAsync(HttpContext context)
        {
            LogoutCalled = true;
            return Task.FromResult(Logout);
        }
    }
}
