using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Exercises the original keyboard, header, cookie and storage controls with bounded CSP provenance.</summary>
internal static class LoginKeyboardJourney
{
    private const string LoginPath = "/account/login";
    private const string FormPolicy = "form-action 'self'";
    private const string CspHeader = "content-security-policy";
    private const string CacheHeader = "cache-control";
    private const string ReferrerHeader = "referrer-policy";
    private const string NoReferrer = "no-referrer";
    private const string Tab = "Tab";
    private const string Enter = "Enter";
    private const string Space = "Space";
    private const string SkipLabel = "Skip to main content";
    private const string Focus = ":focus";
    private const string Main = "#main";
    private const string SkipLink = ".skip-link";
    private const string Wordmark = ".wordmark";
    private const string ThemeId = "theme";
    private const string IdAttribute = "id";
    private const string UsernameId = "username";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Autocomplete = "autocomplete";
    private const string PasswordAutocomplete = "current-password";
    private const string Interaction = "input[name=Interaction]";
    private const string UsernameLabel = "Username";
    private const string PasswordLabel = "Password";
    private const string ShowPassword = "#show-password";
    private const string HidePasswordLabel = "Hide password";
    private const string TextType = "text";
    private const string TypeAttribute = "type";
    private const string PasswordType = "password";
    private const string Submit = "#submit";
    private const string CallbackSuffix = "**";
    private const string Body = "body";
    private const string SuccessBody = "Signed in successfully";
    private const string SessionCookie = "__Host-identity-pilot";
    private const string AccessToken = "access_token";
    private const string IdToken = "id_token";
    private const string WordmarkScript = "element => element.tagName === 'SPAN' && !element.hasAttribute('tabindex') && !element.hasAttribute('role')";
    private const string SessionStorageScript = "Object.keys(sessionStorage).length === 0";
    private const string NoninteractiveWordmark = "The wordmark must remain noninteractive text until a home route exists.";
    private const string UnexpectedResult = "Unexpected relying-party result page.";
    private const string SingleSession = "Expected exactly one application session cookie.";
    private const string UnexpectedStorage = "Unexpected session storage content.";
    private const int CompletionSeconds = 15;

    /// <summary>Uses real issuer credentials and callback completion without weakening CSP or token-secrecy assertions.</summary>
    internal static async Task RequireAsync(IssuerEnvironment environment)
    {
        await using var session = await BrowserSession.CreateAsync();
        var context = session.Context;
        var page = await context.NewPageAsync();
        var violations = await LoginCspEvidence.ObserveAsync(page);
        var flow = environment.RelyingParty.Prepare(exchange: true);
        await NavigateAsync(environment, page, flow, violations);
        await FocusAsync(page, violations);
        await CredentialsAsync(environment, page, flow, violations);
        await CompleteAsync(environment, page, flow, violations);
        await StorageAsync(environment, page, context, flow);
    }

    /// <summary>Requires the exact login route and restrictive response headers.</summary>
    private static async Task NavigateAsync(IssuerEnvironment environment, IPage page, TestFlow flow, LoginCspEvidence violations)
    {
        await violations.MarkAsync(LoginCspPhase.Navigation);
        var response = await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
        Assert.NotNull(response);
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Equal(LoginPath, new Uri(page.Url).AbsolutePath);
        var headers = await response.AllHeadersAsync();
        Assert.Contains(FormPolicy, headers[CspHeader], StringComparison.Ordinal);
        Assert.True(CacheControlHeaderValue.Parse(headers[CacheHeader]).NoStore);
        Assert.Equal(NoReferrer, headers[ReferrerHeader]);
    }

    /// <summary>Requires real keyboard focus, noninteractive branding and empty storage.</summary>
    private static async Task FocusAsync(IPage page, LoginCspEvidence violations)
    {
        await violations.MarkAsync(LoginCspPhase.KeyboardNavigation);
        await page.Keyboard.PressAsync(Tab);
        Assert.Equal(SkipLabel, await page.Locator(Focus).InnerTextAsync());
        await page.Keyboard.PressAsync(Enter);
        await Assertions.Expect(page.Locator(Main)).ToBeFocusedAsync();
        await page.Locator(SkipLink).FocusAsync();
        Assert.True(await page.Locator(Wordmark).EvaluateAsync<bool>(WordmarkScript),
            NoninteractiveWordmark);
        await page.Keyboard.PressAsync(Tab);
        Assert.Equal(ThemeId, await page.Locator(Focus).GetAttributeAsync(IdAttribute));
        Assert.Equal(UsernameId, await page.Locator(Username).GetAttributeAsync(Autocomplete));
        Assert.Equal(PasswordAutocomplete, await page.Locator(Password).GetAttributeAsync(Autocomplete));
        await BrowserJourneys.RequireEmptyStorageAsync(page);
    }

    /// <summary>Enters credentials and toggles visibility through the original accessible controls.</summary>
    private static async Task CredentialsAsync(IssuerEnvironment environment, IPage page, TestFlow flow, LoginCspEvidence violations)
    {
        flow.InteractionId = await page.Locator(Interaction).InputValueAsync();
        await page.Keyboard.PressAsync(Tab);
        await Assertions.Expect(page.Locator(Username)).ToBeFocusedAsync();
        await violations.MarkAsync(LoginCspPhase.Username);
        await page.GetByLabel(UsernameLabel, new()
        {
            Exact = true
        }).FillAsync(environment.Username);
        await page.Keyboard.PressAsync(Tab);
        await Assertions.Expect(page.Locator(Password)).ToBeFocusedAsync();
        await violations.MarkAsync(LoginCspPhase.Password);
        await page.GetByLabel(PasswordLabel, new()
        {
            Exact = true
        }).FillAsync(environment.Password);
        await page.Keyboard.PressAsync(Tab);
        await Assertions.Expect(page.Locator(ShowPassword)).ToBeFocusedAsync();
        await violations.MarkAsync(LoginCspPhase.ShowPassword);
        await page.Keyboard.PressAsync(Space);
        Assert.Equal(TextType, await page.Locator(Password).GetAttributeAsync(TypeAttribute));
        await violations.MarkAsync(LoginCspPhase.HidePassword);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = HidePasswordLabel,
            Exact = true
        }).PressAsync(Space);
        Assert.Equal(PasswordType, await page.Locator(Password).GetAttributeAsync(TypeAttribute));
    }

    /// <summary>Submits by keyboard and requires real protocol completion with no CSP violations.</summary>
    private static async Task CompleteAsync(IssuerEnvironment environment, IPage page, TestFlow flow, LoginCspEvidence violations)
    {
        await violations.MarkAsync(LoginCspPhase.Submit);
        await page.Keyboard.PressAsync(Tab);
        await Assertions.Expect(page.Locator(Submit)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync(Enter);
        await page.WaitForURLAsync(environment.RelyingParty.Callback + CallbackSuffix);
        Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(CompletionSeconds), TestContext.Current.CancellationToken), flow.ValidationFailure);
        Assert.True(await page.Locator(Body).InnerTextAsync() == SuccessBody, UnexpectedResult);
        violations.RequireEmpty();
    }

    /// <summary>Requires the secure application cookie and absence of browser-held tokens or secrets.</summary>
    private static async Task StorageAsync(IssuerEnvironment environment, IPage page, IBrowserContext context, TestFlow flow)
    {
        var cookies = await context.CookiesAsync();
        var sessions = cookies.Where(cookie => cookie.Name == SessionCookie).ToArray();
        Assert.True(sessions.Length == 1, SingleSession);
        var cookie = sessions[0];
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteAttribute.Lax, cookie.SameSite);
        var storage = await context.StorageStateAsync();
        SafeAssertions.ContainsNoSensitiveMaterial(storage, AccessToken, IdToken, environment.Password, environment.ClientSecret);
        Assert.True(await page.EvaluateAsync<bool>(SessionStorageScript), UnexpectedStorage);
        await BrowserJourneys.RequireNoTokensAsync(page, context, environment, flow);
    }
}
