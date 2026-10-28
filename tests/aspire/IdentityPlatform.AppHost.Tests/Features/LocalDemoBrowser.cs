using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Exercises normal demo pages over real browser TLS without authority fixtures, certificate overrides or screenshots.</summary>
internal sealed class LocalDemoBrowser : IAsyncDisposable
{
    private const int SuccessStatus = 200;
    private const int UnauthorizedStatus = 401;
    private const string SecurityPath = "/account/administrator/security";
    private const int RecoveryCodeCount = 10;
    private const string EnrollmentPath = "/account/administrator/enrollment";
    private const string SetupKey = "#setup-key";
    private const string RecoveryCodes = ".recovery-codes code";
    private const string LoginPath = "/bff/login";
    private const string StartLabel = "Start sign-in";
    private const string SessionPath = "/bff/session";
    private const string AuthenticatedField = "authenticated";
    private const string LogoutPath = "/bff/logout";
    private const string IssuerChoice = "button[name=Mode][value=issuer]";
    private const string LocalChoice = "button[name=Mode][value=local]";
    private const string Main = "main";
    private const string LocalSuccess = "session has ended locally";
    private const string IssuerLink = "#issuer-sign-out";
    private const string ConfirmIssuer = "Sign out this issuer session";
    private const string Unverified = "Issuer sign-out result is unverified.";
    private const string Issuer = "https://issuer.dev.localhost:17443";
    private const string Client = "https://bff.dev.localhost:17444";
    private const string UsernameField = "#username";
    private const string PasswordField = "#password";
    private const string Submit = "#submit";
    private const string CodeField = "#code";
    private readonly IPlaywright playwright;
    private readonly IBrowser browser;
    private readonly IBrowserContext context;
    private readonly IPage page;
    private readonly HashSet<string> usedCodes = new(StringComparer.Ordinal);
    private string? seed;

    /// <summary>Retains sensitive browser state only in memory until the context closes.</summary>
    private LocalDemoBrowser(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page)
    {
        this.playwright = playwright;
        this.browser = browser;
        this.context = context;
        this.page = page;
    }

    /// <summary>Launches Chromium with ordinary certificate validation, the engine available to the Windows demo audience.</summary>
    internal static async Task<LocalDemoBrowser> CreateAsync()
    {
        var playwright = await Playwright.CreateAsync();
        IBrowser? browser = null;
        try
        {
            browser = await playwright.Chromium.LaunchAsync();
            var context = await browser.NewContextAsync();
            LocalDemoNavigation.Configure(context);
            return new(playwright, browser, context, await context.NewPageAsync());
        }
        catch
        {
            if (browser is not null)
            {
                await browser.DisposeAsync();
            }
            playwright.Dispose();
            throw;
        }
    }

    /// <summary>Uses the pending bootstrap account and a real independently computed TOTP, then observes one-time recovery output.</summary>
    internal async Task EnrollAsync(LocalDemoRuntime runtime)
    {
        var response = await page.GotoAsync(Issuer + EnrollmentPath);
        Assert.Equal(SuccessStatus, response!.Status);
        await CredentialsAsync(runtime);
        await page.Locator(SetupKey).WaitForAsync();
        seed = await page.Locator(SetupKey).InputValueAsync();
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        usedCodes.Add(code);
        await page.Locator(CodeField).FillAsync(code);
        await page.Locator(Submit).ClickAsync();
        await page.Locator(RecoveryCodes).First.WaitForAsync();
        Assert.Equal(RecoveryCodeCount, await page.Locator(RecoveryCodes).CountAsync());
    }

    /// <summary>Every explicit login must render a new password and MFA form, including when an issuer cookie already exists.</summary>
    internal async Task SignInAsync(LocalDemoRuntime runtime)
    {
        var evidence = new LocalDemoSignInEvidence();
        page.Response += evidence.Observe;
        try
        {
            await SignInAsync(runtime, evidence);
        }
        finally { page.Response -= evidence.Observe; }
    }

    /// <summary>Records finite failure observations while the exact post-MFA navigation owns its already-configured twenty-second budget.</summary>
    private async Task SignInAsync(LocalDemoRuntime runtime, LocalDemoSignInEvidence evidence)
    {
        string? code = null;
        long sampled = 0;
        try
        {
            evidence.Current = LocalDemoSignInEvidence.Stage.Navigation;
            var response = await page.GotoAsync(Client + LoginPath);
            Assert.Equal(SuccessStatus, response!.Status);
            evidence.Current = LocalDemoSignInEvidence.Stage.Start;
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = StartLabel,
                Exact = true
            }).ClickAsync();
            evidence.Current = LocalDemoSignInEvidence.Stage.Password;
            await CredentialsAsync(runtime);
            evidence.Current = LocalDemoSignInEvidence.Stage.FactorForm;
            await page.Locator(CodeField).WaitForAsync();
            evidence.Current = LocalDemoSignInEvidence.Stage.CodeSelection;
            sampled = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            code = FactorOracle.UnusedCode(seed!, usedCodes);
            usedCodes.Add(code);
            evidence.Current = LocalDemoSignInEvidence.Stage.FactorSubmission;
            await page.Locator(CodeField).FillAsync(code);
            evidence.Current = LocalDemoSignInEvidence.Stage.Callback;
            await LocalDemoNavigation.SubmitAsync(page, page.Locator(Submit), Client + LoginPath);
            evidence.Current = LocalDemoSignInEvidence.Stage.Status;
            await RequireStatusAsync(true);
        }
        catch (Exception error)
        {
            throw await evidence.FailureAsync(page, error, seed, code, sampled);
        }
    }

    /// <summary>Submits only the actual page's password form; it cannot grant or inject an authenticated principal.</summary>
    private async Task CredentialsAsync(LocalDemoRuntime runtime)
    {
        await page.Locator(UsernameField).FillAsync(LocalDemoRuntime.Username);
        await page.Locator(PasswordField).FillAsync(runtime.Password);
        await page.Locator(Submit).ClickAsync();
    }

    /// <summary>Checks actual server-held authority through the normal nonrenewing status endpoint without printing identity fields.</summary>
    internal Task RequireStatusAsync(bool authenticated) => RequireStatusAsync(page, authenticated);

    /// <summary>Checks the same protected-session contract for the live browser or an isolated replay context.</summary>
    private static async Task RequireStatusAsync(IPage page, bool authenticated)
    {
        var response = await page.GotoAsync(Client + SessionPath);
        Assert.Equal(SuccessStatus, response!.Status);
        using var body = JsonDocument.Parse(await response.TextAsync());
        Assert.Equal(authenticated, body.RootElement.GetProperty(AuthenticatedField).GetBoolean());
    }

    /// <summary>Replays stale cookies separately so the main browser retains the logout generation required for its next sign-in.</summary>
    internal async Task RequireClientReplayDeniedAsync(Cookie[] cookies)
    {
        await using var replay = await browser.NewContextAsync();
        await replay.AddCookiesAsync(cookies);
        await RequireStatusAsync(await replay.NewPageAsync(), false);
    }

    /// <summary>Uses native browser Origin/antiforgery and separately confirms issuer logout after local success.</summary>
    internal async Task LogoutAsync(bool issuer)
    {
        await page.GotoAsync(Client + LogoutPath);
        await page.Locator(issuer ? IssuerChoice : LocalChoice).ClickAsync();
        await page.Locator(Main).Filter(new()
        {
            HasText = LocalSuccess
        }).WaitForAsync();
        if (issuer)
        {
            await page.Locator(IssuerLink).ClickAsync();
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = ConfirmIssuer,
                Exact = true
            }).ClickAsync();
            await page.Locator(Main).Filter(new()
            {
                HasText = Unverified
            }).WaitForAsync();
        }
        await RequireStatusAsync(false);
    }

    /// <summary>Replays an actual issuer cookie in a separate context against current server-side authority in the same graph.</summary>
    internal async Task RequireIssuerReplayAsync(Cookie[] cookies, bool authenticated)
    {
        await using var replay = await browser.NewContextAsync();
        await replay.AddCookiesAsync(cookies);
        var page = await replay.NewPageAsync();
        var response = await page.GotoAsync(Issuer + SecurityPath);
        Assert.Equal(authenticated ? SuccessStatus : UnauthorizedStatus, response!.Status);
    }

    /// <summary>Retains actual protected cookies in memory for old-session and new-run invalidation checks.</summary>
    internal async Task<Cookie[]> CookiesAsync() => (await context.CookiesAsync()).Select(cookie => new Cookie
    {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        HttpOnly = cookie.HttpOnly,
        Secure = cookie.Secure,
        SameSite = cookie.SameSite,
        Expires = cookie.Expires
    }).ToArray();

    /// <summary>Replays authentic earlier cookies without constructing any replacement session authority.</summary>
    internal async Task RestoreAsync(Cookie[] cookies)
    {
        await context.ClearCookiesAsync();
        await context.AddCookiesAsync(cookies);
    }

    /// <summary>Closes network/browser consumers before clearing the test-only retained enrollment seed.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await context.DisposeAsync();
        }
        finally
        {
            try
            {
                await browser.DisposeAsync();
            }
            finally { playwright.Dispose(); seed = null; usedCodes.Clear(); }
        }
    }
}
