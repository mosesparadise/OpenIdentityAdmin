using System.Net;
using System.Text.RegularExpressions;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Exercises real native Razor forms without bypassing antiforgery or retaining secret response diagnostics.</summary>
internal static class AdministratorJourney
{
    private const string UsernameField = "Username";
    private const string PasswordField = "Password";
    internal const string CodeField = "Code";
    private const string SetupInput = "setup-key";
    private const string LocalContinuationRequired = "The account continuation must remain local.";

    internal const string Enrollment = "/account/administrator/enrollment";
    internal const string Login = "/account/administrator/login";
    internal const string Mfa = "/account/administrator/mfa";
    internal const string Security = "/account/administrator/security";
    internal const string Antiforgery = "__RequestVerificationToken";
    internal const string Interaction = "Interaction";
    internal const string SessionCookie = "__Host-identity-administrator";

    /// <summary>Submits native password verification and returns only an admitted exact-origin continuation.</summary>
    internal static async Task<Uri> BeginAsync(AdministratorEnvironment environment, HttpClient browser, bool enrollment)
    {
        var path = enrollment ? Enrollment : Login;
        using var page = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var result = await PostAsync(browser, path, new Dictionary<string, string>
        {
            [Antiforgery] = Input(html, Antiforgery),
            [UsernameField] = environment.Database.Username,
            [PasswordField] = environment.Database.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var location = result.Headers.Location!;
        Assert.False(location.IsAbsoluteUri, LocalContinuationRequired);
        return location;
    }

    /// <summary>Reads the actual random setup key and native anti-forgery binding without capturing the page as an artifact.</summary>
    internal static async Task<AdministratorForm> ReadAsync(HttpClient browser, Uri location, bool enrollment)
    {
        using var result = await browser.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var html = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new AdministratorForm(Input(html, Interaction), Input(html, Antiforgery),
            enrollment ? Input(html, SetupInput, byId: true) : null);
    }

    /// <summary>Posts a factor through the exact browser form; responses remain owned by the calling test.</summary>
    internal static Task<HttpResponseMessage> ConfirmAsync(HttpClient browser, AdministratorForm form, string code, bool enrollment) =>
        PostAsync(browser, enrollment ? Enrollment : Mfa, new Dictionary<string, string>
        {
            [Interaction] = form.Interaction,
            [Antiforgery] = form.Antiforgery,
            [CodeField] = code
        });

    /// <summary>Submits a query-free form with the actual request cancellation token.</summary>
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, IEnumerable<KeyValuePair<string, string>> fields)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await browser.PostAsync(path, content, TestContext.Current.CancellationToken);
    }

    /// <summary>Extracts one exact form input with a bounded parser and fixed diagnostics instead of dumping secret HTML.</summary>
    internal static string Input(string html, string name, bool byId = false)
    {
        const string Prefix = "<input[^>]*";
        const string Identifier = "id";
        const string Name = "name";
        const string Assignment = "=\"";
        const string Suffix = "\"[^>]*value=\"([^\"]*)\"";
        const string Missing = "The required form input was absent or ambiguous.";
        var expression = new Regex(Prefix + (byId ? Identifier : Name) + Assignment + Regex.Escape(name) + Suffix,
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var matches = expression.Matches(html);
        Assert.True(matches.Count == 1, Missing);
        return WebUtility.HtmlDecode(matches[0].Groups[1].Value);
    }

}

/// <summary>Retains synthetic live form bindings in memory only and never includes their values in generated diagnostics.</summary>
internal sealed class AdministratorForm(string interaction, string antiforgery, string? seed)
{
    internal string Interaction { get; } = interaction;
    internal string Antiforgery { get; } = antiforgery;
    internal string? Seed { get; } = seed;
}
