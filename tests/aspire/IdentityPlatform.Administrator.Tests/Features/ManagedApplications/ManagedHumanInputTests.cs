using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises real native antiforgery and independent exact managed form framing over trusted HTTPS.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanInputTests(BootstrapFixture fixture)
{
    private const string Origin = "Origin";
    private const string ForeignOrigin = "https://foreign.example.test";
    private const string Unexpected = "unexpected";
    private const string InvalidQuery = "?unexpected=1";
    private const string Invalid = "invalid";

    /// <summary>Malformed CSRF, origin, field multiplicity, query and foreign-browser requests cannot alter any durable proof; the untouched original form remains usable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealPasswordForm_When_FramingOrBrowserIsInvalid_Then_NoAuthorityOrFailureBudgetChanges(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await ManagedHumanHttp.StartAsync(test);
        var fields = new Dictionary<string, string>(form, StringComparer.Ordinal)
        {
            [ManagedHumanHttp.Username] = test.Http.Environment.Issuer.Account.Journey.Lease.Username,
            [ManagedHumanHttp.Password] = test.Http.Environment.Issuer.Account.Journey.Lease.Password
        };
        var before = await ManagedHumanEvidence.DigestAsync(test);
        var origin = test.Http.Environment.Profile.Issuer.GetLeftPart(UriPartial.Authority);
        await RejectAsync(test, fields.Where(pair => pair.Key != AdministratorJourney.Antiforgery), origin, before);
        await RejectAsync(test, fields.Select(pair => pair.Key == AdministratorJourney.Antiforgery ? new(pair.Key, Invalid) : pair), origin, before);
        await RejectAsync(test, fields, null, before);
        await RejectAsync(test, fields, ForeignOrigin, before);
        await RejectAsync(test, fields.Append(new(ManagedHumanHttp.Flow, form[ManagedHumanHttp.Flow])), origin, before);
        await RejectAsync(test, fields.Append(new(Unexpected, Invalid)), origin, before);
        await RejectAsync(test, fields, origin, before, query: true);
        using var foreign = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = test.Http.Environment.Profile.Issuer };
        using var response = await foreign.GetAsync(QueryHelpers.AddQueryString(ManagedHumanHttp.Path, ManagedHumanHttp.Flow, form[ManagedHumanHttp.Flow]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        await RejectForeignPostAsync(test, fields);
        using var accepted = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(accepted);
    }

    /// <summary>A second genuine managed browser gets its own native CSRF; neither cross-browser form nor CSRF substitution can spend either interaction.</summary>
    private static async Task RejectForeignPostAsync(ManagedCacheTestContext test, Dictionary<string, string> original)
    {
        using var foreign = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = test.Http.Environment.Profile.Issuer };
        using var initial = await foreign.GetAsync(QueryHelpers.AddQueryString(NativeCacheProfile.Authorize,
            ManagedCacheTestContext.Initial().Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))), TestContext.Current.CancellationToken);
        _ = await test.ResumeAsync(initial);
        using var resumed = await foreign.GetAsync(initial.Headers.Location, TestContext.Current.CancellationToken);
        await ManagedHumanHttp.EntryAsync(resumed);
        var form = await ManagedHumanHttp.FormAsync(foreign, resumed.Headers.Location!);
        var before = await ManagedHumanEvidence.DigestAsync(test);
        var substituted = new Dictionary<string, string>(original, StringComparer.Ordinal) { [AdministratorJourney.Antiforgery] = form[AdministratorJourney.Antiforgery] };
        using var flow = await BrowserIssuerHttp.PostAsync(foreign, ManagedHumanHttp.Path, substituted);
        Assert.Equal(HttpStatusCode.BadRequest, flow.StatusCode);
        substituted[ManagedHumanHttp.Flow] = form[ManagedHumanHttp.Flow];
        substituted[AdministratorJourney.Antiforgery] = original[AdministratorJourney.Antiforgery];
        using var csrf = await BrowserIssuerHttp.PostAsync(foreign, ManagedHumanHttp.Path, substituted);
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        substituted[AdministratorJourney.Antiforgery] = form[AdministratorJourney.Antiforgery];
        using var own = await BrowserIssuerHttp.PostAsync(foreign, ManagedHumanHttp.Path, substituted);
        await ManagedHumanHttp.EntryAsync(own);
    }

    /// <summary>Sends deliberately invalid real form requests and requires local denial with no durable effect or authority cookie.</summary>
    private static async Task RejectAsync(ManagedCacheTestContext test, IEnumerable<KeyValuePair<string, string>> fields, string? origin, string before, bool query = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ManagedHumanHttp.Path + (query ? InvalidQuery : string.Empty))
        {
            Content = new FormUrlEncodedContent(fields)
        };
        if (origin is not null)
        {
            request.Headers.Add(Origin, origin);
        }
        using var response = await test.Http.IssuerClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        ManagedHumanHttp.NoAuthorityCookie(response);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }
}
