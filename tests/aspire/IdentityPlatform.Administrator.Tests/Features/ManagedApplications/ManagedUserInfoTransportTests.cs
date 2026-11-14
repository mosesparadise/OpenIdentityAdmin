using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises real native bearer errors and raw transport rejection while retaining an independently usable managed credential.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedUserInfoTransportTests(BootstrapFixture fixture)
{
    private const string AccessField = "access_token";
    private const string IdentityField = "id_token";
    private const string Malformed = "untrusted.token";
    private const string TokenQuery = "?access_token=synthetic";
    private const string Bearer = "Bearer";
    private const string InvalidRequest = "invalid_request";
    private const string CookieHeader = "Set-Cookie";
    private const string ErrorPrefix = "error=\"";
    private const string ErrorSuffix = "\", error_description=\"The request could not be completed.\"";

    /// <summary>Missing, malformed, wrong-type and fixed-administrator credentials cannot use UserInfo; query and ambiguous transports never fall back.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UsableManagedAccess_When_MissingWrongProfileOrAmbiguousCredentialsArrive_Then_OnlyOriginalBearerRemainsUsable(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        using var exchange = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var access = body.RootElement.GetProperty(AccessField).GetString()!;
        var identity = body.RootElement.GetProperty(IdentityField).GetString()!;
        var subject = Assert.Single((await test.SnapshotAsync()).Interactions).UserId!;
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var missing = await test.Http.IssuerClient.GetAsync(ManagedConsentTest.UserInfo, TestContext.Current.CancellationToken);
        await ErrorAsync(missing, HttpStatusCode.Unauthorized, null);
        foreach (var credential in new[] { Malformed, identity })
        {
            using var denied = await ManagedConsentTest.ReadAsync(test, credential);
            await ManagedConsentTest.DeniedAsync(denied);
        }
        using var fixedProfile = await test.Http.FixedIssuerBearerAsync(ManagedConsentTest.UserInfo);
        await ManagedConsentTest.DeniedAsync(fixedProfile);
        await AmbiguousAsync(test, access, form: false);
        await AmbiguousAsync(test, access, form: true);
        using var control = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.RequireAsync(control, subject, null, false);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        using var logout = await BrowserLogoutHttp.PostAsync(test.Http.Browser, await BrowserLogoutHttp.EntryAsync(test.Http.Browser));
        await BrowserLogoutHttp.CommittedAsync(logout);
    }

    /// <summary>Supplies a valid header together with a forbidden query or form token and requires rejection of the entire request.</summary>
    private static async Task AmbiguousAsync(ManagedCacheTestContext test, string access, bool form)
    {
        using var request = new HttpRequestMessage(form ? HttpMethod.Post : HttpMethod.Get,
            ManagedConsentTest.UserInfo + (form ? string.Empty : TokenQuery));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        if (form)
        {
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { [AccessField] = access });
        }
        using var response = await test.Http.IssuerClient.SendAsync(request, TestContext.Current.CancellationToken);
        await ErrorAsync(response, HttpStatusCode.BadRequest, InvalidRequest);
    }

    /// <summary>Requires exact finite native RFC bearer challenges and empty no-store responses without reflecting credentials.</summary>
    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string? error)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.False(response.Headers.Contains(CookieHeader));
        Assert.Null(response.Headers.Location);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal(Bearer, challenge.Scheme);
        Assert.Equal(error is null ? null : ErrorPrefix + error + ErrorSuffix, challenge.Parameter);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }
}
