using System.Net;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class InteractionTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Tampering = ["missing-antiforgery", "wrong-antiforgery", "other-browser", "expired", "consumed"];
    public static IEnumerable<TheoryDataRow<string, string>> InvalidForms =>
        from provider in Providers
        from tampering in Tampering
        select new TheoryDataRow<string, string>(provider, tampering).WithTrait("Provider", provider);

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Independent_browser_login_does_not_change_original_sessions_authentication_time(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var firstBrowser = IssuerEnvironment.CreateBrowserClient();
        using var secondBrowser = IssuerEnvironment.CreateBrowserClient();
        var first = await environment.AuthorizeAsync(firstBrowser, exchange: true);
        await Task.Delay(TimeSpan.FromSeconds(1.1), TestContext.Current.CancellationToken);
        var second = await environment.AuthorizeAsync(secondBrowser, exchange: true);
        Assert.NotEqual(first.ExpectedSessionId, second.ExpectedSessionId);
        var reuse = await environment.AuthorizeAsync(firstBrowser, exchange: true, prompt: "none");
        Assert.Equal(first.ExpectedSessionId, reuse.ExpectedSessionId);
        Assert.True(reuse.Validated);
        Assert.Null(reuse.InteractionId); // No fresh form was rendered to satisfy this silent request.
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Anonymous_second_tab_gets_safe_restart_after_first_tab_changes_antiforgery_identity(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var first = await TestLogin.BeginAsync(environment, browser);
        var second = await TestLogin.BeginAsync(environment, browser);
        using var accepted = await first.SubmitAsync(environment, browser);
        Assert.Equal("/account/complete", accepted.RequestMessage!.RequestUri!.AbsolutePath);
        using var rejected = await second.SubmitAsync(environment, browser);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("This request cannot be completed. Return to your application and start a new sign-in.",
            await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(second.Flow.Code);
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true, prompt: "login")).Validated);
    }

    [Theory]
    [MemberData(nameof(InvalidForms))]
    public async Task Forged_or_stale_form_cannot_create_a_session(string provider, string tampering)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        using var other = IssuerEnvironment.CreateBrowserClient();
        var login = await TestLogin.BeginAsync(environment, browser);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken);
        var row = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
        if (tampering == "expired")
        {
            row.ExpiresAt = 1;
        }
        if (tampering == "consumed")
        {
            row.Consumed = true;
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var response = await login.SubmitAsync(environment, tampering == "other-browser" ? other : browser,
            antiforgery: tampering == "missing-antiforgery" ? "" : tampering == "wrong-antiforgery" ? "forged" : null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(login.Flow.Code);
        Assert.Equal(sessions, await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken));
        await database.Entry(row).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Null(row.CompletedSessionId);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Unknown_account_and_wrong_password_have_generic_errors_and_no_password_echo(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var login = await TestLogin.BeginAsync(environment, browser);
        const string sentinel = "incorrect-synthetic-password-sentinel";
        using var unknown = await login.SubmitAsync(environment, browser, username: "unknown.synthetic.user", password: sentinel);
        using var wrong = await login.SubmitAsync(environment, browser, password: sentinel);
        foreach (var response in new[] { unknown, wrong })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Unable to sign in. Check your details or start a new sign-in request.", html, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, html, StringComparison.Ordinal);
            Assert.DoesNotContain(environment.Password, html, StringComparison.Ordinal);
        }
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audits = await database.Set<SecurityAuditIntent>().Where(value => value.Event == "password-verification" && value.Outcome == "denied")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(audits.Length >= 2);
        Assert.All(audits, audit => { Assert.Equal("anonymous", audit.ActorType); Assert.Null(audit.ActorId); });
        // A successful fresh login resets the native failure counter through SignInManager.
        await environment.AuthorizeAsync(browser);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Fresh_login_replaces_the_old_session_and_completed_tab_cannot_switch_identity(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var first = await TestLogin.BeginAsync(environment, browser, prompt: "login");
        using var completedFirst = await first.SubmitAsync(environment, browser);
        Assert.Equal("/account/complete", completedFirst.RequestMessage!.RequestUri!.AbsolutePath);
        var firstCompletion = completedFirst.RequestMessage.RequestUri;
        // The second form is rendered after the first login so its antiforgery identity is current.
        var second = await TestLogin.BeginAsync(environment, browser, prompt: "login");
        using var completedSecond = await second.SubmitAsync(environment, browser);
        Assert.Equal("/account/complete", completedSecond.RequestMessage!.RequestUri!.AbsolutePath);
        using var staleTab = await browser.GetAsync(firstCompletion, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, staleTab.StatusCode);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var oldInteraction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == first.Interaction, TestContext.Current.CancellationToken);
        var oldSession = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == oldInteraction.CompletedSessionId, TestContext.Current.CancellationToken);
        Assert.True(oldSession.Revoked);
        Assert.Empty(first.Flow.Code);
        Assert.Empty(second.Flow.Code);
    }
}
