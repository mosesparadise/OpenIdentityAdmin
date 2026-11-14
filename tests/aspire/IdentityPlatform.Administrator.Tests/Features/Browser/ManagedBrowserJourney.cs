using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses the same UI-created registration for genuine browser password, MFA, consent and server-side native token exchange.</summary>
internal static class ManagedBrowserJourney
{
    private const string AccountPath = "/account/application/sign-in";
    private const string PasswordHeading = "Sign in";
    private const string FactorHeading = "Verify your authenticator";
    private const string ConsentHeading = "Review access";
    private const string DeniedHeading = "Sign-in cancelled";
    private const string CompleteHeading = "Application sign-in complete";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Code = "#code";
    private const string Submit = "#submit";
    private const string Approve = "Allow sign-in";
    private const string Deny = "Cancel";
    private const string Enter = "Enter";
    private const string Tab = "Tab";
    private const string PasswordFocused = "() => document.activeElement?.id === 'password'";
    private const string IssuerCookie = "__Host-identity-administrator";
    private const string SessionCookie = "__Host-identity-admin-session";
    private const string GenerationCookie = "__Host-identity-admin-generation";
    private const string CorrelationCookie = "__Host-managed-test-rp";
    private const int AuthorityCookies = 3;
    private const int CompletionSeconds = 15;

    /// <summary>Requires fresh proof despite existing issuer authority, explicit consent and unchanged administrator cookies.</summary>
    internal static async Task<ManagedBrowserFlow> RunAsync(IPage page, ManagedBrowserRelyingParty relyingParty,
        ApplicationEditorRegistration registration, string username, string password, Func<Task<string>> factor,
        string subject, bool approve = true, Func<IPage, Task>? presentation = null, ManagedTokenProjection? projection = null)
    {
        var authority = await AuthorityAsync(page);
        var flow = relyingParty.Prepare(registration, subject, projection);
        try
        {
            await page.GotoAsync(relyingParty.Start.AbsoluteUri);
            await CredentialsAsync(page, relyingParty.Issuer, username, password, factor, presentation);
            await DecisionAsync(page, flow, relyingParty, approve, presentation);
        }
        finally
        {
            ApplicationEditorResponse.Report(() => flow.Diagnostic.Describe(page.IsClosed ? null : page.Url,
                relyingParty.Issuer, relyingParty.Origin, flow.Callbacks, flow.Exchanges,
                flow.Completion.Task.IsCompletedSuccessfully ? flow.Completion.Task.Result : null),
                value => TestContext.Current.TestOutputHelper?.WriteLine(value));
        }
        var currentAuthority = await AuthorityAsync(page);
        Assert.True(authority.SequenceEqual(currentAuthority));
        await BrowserSecretOracle.StorageAsync(page, flow.Secrets);
        return flow;
    }

    /// <summary>Traverses the actual native password/MFA forms using keyboard submission; an existing issuer cookie cannot skip either form.</summary>
    private static async Task CredentialsAsync(IPage page, Uri issuer, string username, string password,
        Func<Task<string>> factor, Func<IPage, Task>? presentation)
    {
        await HeadingAsync(page, PasswordHeading);
        var location = new Uri(page.Url);
        Assert.True(location.GetLeftPart(UriPartial.Authority) == issuer.GetLeftPart(UriPartial.Authority) && location.AbsolutePath == AccountPath);
        Assert.True(await page.Locator(Password).IsVisibleAsync());
        await PresentAsync(page, presentation);
        await page.Locator(Username).FocusAsync();
        await page.Keyboard.InsertTextAsync(username);
        await page.Keyboard.PressAsync(Tab);
        Assert.True(await page.EvaluateAsync<bool>(PasswordFocused));
        await page.Keyboard.InsertTextAsync(password);
        await SubmitAsync(page, page.Locator(Submit));
        await HeadingAsync(page, FactorHeading);
        Assert.True(await page.Locator(Code).IsVisibleAsync() && await page.Locator(Password).CountAsync() == 0);
        await PresentAsync(page, presentation);
        await page.Locator(Code).FillAsync(await factor());
        await SubmitAsync(page, page.Locator(Submit));
        await HeadingAsync(page, ConsentHeading);
        Assert.True(await page.Locator(Code).CountAsync() == 0 && await page.Locator(Password).CountAsync() == 0);
        await PresentAsync(page, presentation);
    }

    /// <summary>Checks explicit terminal denial or one correlated callback and one independently verified native exchange.</summary>
    private static async Task DecisionAsync(IPage page, ManagedBrowserFlow flow, ManagedBrowserRelyingParty relyingParty,
        bool approve, Func<IPage, Task>? presentation)
    {
        await SubmitAsync(page, page.GetByRole(AriaRole.Button, new()
        {
            Name = approve ? Approve : Deny,
            Exact = true
        }));
        if (approve)
        {
            await page.WaitForURLAsync(relyingParty.Complete.AbsoluteUri);
            await HeadingAsync(page, CompleteHeading);
            Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(CompletionSeconds), TestContext.Current.CancellationToken));
            Assert.Equal(1, flow.Callbacks);
            Assert.Equal(1, flow.Exchanges);
            Assert.DoesNotContain(await page.Context.CookiesAsync(), cookie => cookie.Name == CorrelationCookie);
        }
        else
        {
            await HeadingAsync(page, DeniedHeading);
            Assert.Equal(0, flow.Callbacks);
            Assert.Equal(0, flow.Exchanges);
            Assert.False(flow.Completion.Task.IsCompleted);
            Assert.Equal(0, await page.Locator(Submit).CountAsync());
            await PresentAsync(page, presentation);
        }
    }

    /// <summary>Retains only the exact existing administrator authority cookie values in private memory.</summary>
    private static async Task<string[]> AuthorityAsync(IPage page)
    {
        var cookies = (await page.Context.CookiesAsync()).Where(cookie => cookie.Name is IssuerCookie or SessionCookie or GenerationCookie)
            .OrderBy(cookie => cookie.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(AuthorityCookies, cookies.Length);
        Assert.True(cookies.All(cookie => cookie.HttpOnly && cookie.Secure));
        return cookies.Select(cookie => cookie.Value).ToArray();
    }

    /// <summary>Invokes the retained-engine presentation oracle only when supplied; native demos share functional ceremony without duplicate axe claims.</summary>
    private static Task PresentAsync(IPage page, Func<IPage, Task>? presentation) => presentation?.Invoke(page) ?? Task.CompletedTask;

    /// <summary>Submits one explicit native action through a real keyboard without retrying the ceremony.</summary>
    private static async Task SubmitAsync(IPage page, ILocator action)
    {
        await action.FocusAsync();
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Waits for one finite visible product outcome without including any protocol URL or private form in an assertion.</summary>
    private static Task HeadingAsync(IPage page, string heading) => page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
}
