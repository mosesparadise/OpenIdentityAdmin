using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Exercises native antiforgery, exact request shapes and cross-browser bindings through real HTTPS pages.</summary>
[Collection("administrator-bootstrap")]
public sealed class FormTests(BootstrapFixture fixture)
{
    private const string Username = "Username";
    private const string Password = "Password";
    private const string Code = "Code";
    private const string Unexpected = "Unexpected";
    private const string Marker = "synthetic-private-marker";
    private const string QueryDuplicate = "?Interaction=";
    private static readonly string[] MalformedCodes = ["12345", "1234567", " 123456", "١٢٣٤٥٦"];

    /// <summary>Native forms reject trimmed, non-ASCII and wrong-width factors without consuming proof; exact digits remain usable.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BoundFactorForm_When_OtpShapeIsMalformed_Then_NoProofIsConsumedAndCanonicalInputCanComplete(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        foreach (var code in MalformedCodes)
        {
            using var denied = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<AdministratorReplay>().ToArrayAsync(TestContext.Current.CancellationToken));
        using var accepted = await AdministratorJourney.ConfirmAsync(browser, form,
            FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), enrollment: true);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>Missing CSRF and ambiguous or extra credentials cannot allocate any restricted interaction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PasswordForm_When_CsrfOrFieldShapeIsInvalid_Then_NoAdmissionOrSecretReflectionOccurs(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        using var page = await browser.GetAsync(AdministratorJourney.Enrollment, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var fields = new Dictionary<string, string>
        {
            [Username] = environment.Database.Username,
            [Password] = environment.Database.Password,
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery)
        };
        await DeniedAsync(browser, AdministratorJourney.Enrollment, fields.Where(pair => pair.Key != AdministratorJourney.Antiforgery));
        await DeniedAsync(browser, AdministratorJourney.Enrollment, fields.Append(new(Unexpected, Marker)));
        await DeniedAsync(browser, AdministratorJourney.Enrollment, fields.Append(new(Username, Marker)));
        await DeniedAsync(browser, AdministratorJourney.Enrollment + QueryDuplicate + Marker, fields);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<AdministratorInteraction>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
    }

    /// <summary>Another browser's valid antiforgery token cannot authorize the owner's restricted interaction or consume its proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BoundEnrollment_When_AnotherBrowserSubmitsProof_Then_OwnerCanStillComplete(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var ownerCookies = new CookieContainer();
        using var owner = environment.Browser(ownerCookies);
        var location = await AdministratorJourney.BeginAsync(environment, owner, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(owner, location, enrollment: true);
        using var stranger = environment.Browser(new CookieContainer(), secondary: true);
        using var page = await stranger.GetAsync(AdministratorJourney.Enrollment, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var denied = await AdministratorJourney.PostAsync(stranger, AdministratorJourney.Enrollment, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery),
            [AdministratorJourney.Interaction] = form.Interaction,
            [Code] = code
        });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var completed = await AdministratorJourney.ConfirmAsync(owner, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.NotNull(ownerCookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
    }

    /// <summary>Checks fixed failures without printing supplied secrets, cookie values or secret-bearing HTML.</summary>
    private static async Task DeniedAsync(HttpClient browser, string path, IEnumerable<KeyValuePair<string, string>> fields)
    {
        using var response = await AdministratorJourney.PostAsync(browser, path, fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        const string Reflected = "A submitted private marker was reflected in the failure response.";
        Assert.False(html.Contains(Marker, StringComparison.Ordinal), Reflected);
    }
}
