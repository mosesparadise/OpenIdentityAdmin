using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Drives the public device and browser over separate HTTPS clients with genuine password, MFA, consent and native tokens.</summary>
[Collection("administrator-bootstrap")]
public sealed class PublicDeviceJourneyTests(BootstrapFixture fixture)
{
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string StrictOrigin = "strict-origin";
    private const string Bearer = "Bearer";
    private const string Completion = "Return to your device.";
    private const string Client = "managed-display";
    private const string Display = "Meeting room display";
    private const string DevicePath = "/connect/device";
    private const string TokenPath = "/connect/token";
    private const string UserPath = "/account/device";
    private const string VerificationPath = "/connect/verify";
    private const string UserField = "UserCode";
    private const string Scope = "openid";
    private const string FullScope = "openid email orders.read";
    private const string AccessType = "at+jwt";
    private const string IdentityType = "JWT";
    private const int Lifetime = 600;
    private const int Pace = 10;
    private const int AfterBrowserDeadline = 310;
    private const string UserInfoPath = "/connect/userinfo";

    /// <summary>Submits two genuine native codes on the issuer, explicitly approves or refuses each display and observes the public token endpoint.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RegisteredPublicDevice_When_UserApprovesOrDeniesWithFreshMfa_Then_OnlyApprovedDeviceCanRedeemOnce(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        await using (var registration = test.Http.Scope())
        {
            var apps = registration.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = DeviceClientRegistration.Descriptor(Client, Display);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Email);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + ManagedConsentTest.ApiScope);
            var app = await apps.CreateAsync(descriptor, TestContext.Current.CancellationToken);
            var database = registration.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Add(new ApplicationPolicy { ApplicationId = (await apps.GetIdAsync(app, TestContext.Current.CancellationToken))!, Profile = DevicePollingWindow.Profile, Revision = 1 });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var device = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = test.Http.Environment.Profile.Issuer };
        var subject = await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.Address, verified: true);
        foreach (var (approve, scopes) in new[] { (true, Scope), (true, FullScope), (false, FullScope) })
        {
            var pair = await IssueAsync(device, scopes);
            await PollAsync(device, pair.Device, OpenIddictConstants.Errors.SlowDown);
            Advance(test);
            await PollAsync(device, pair.Device, OpenIddictConstants.Errors.AuthorizationPending);
            await DecideAsync(test, pair.User, approve, scopes == FullScope);
            Advance(test, approve ? AfterBrowserDeadline : Pace);
            var tokens = await PollAsync(device, pair.Device, approve ? null : OpenIddictConstants.Errors.AccessDenied);
            if (approve)
            {
                var access = tokens!.Value.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
                await RequireTokensAsync(test, device, tokens.Value, subject, scopes == FullScope);
                await RequireUserInfoAsync(device, access, subject, scopes == FullScope);
                Advance(test);
                await PollAsync(device, pair.Device, OpenIddictConstants.Errors.InvalidGrant);
            }
        }
    }

    /// <summary>Uses the actual backchannel endpoint, native nonempty opaque references and the issuer's separate verification URI.</summary>
    private static async Task<(string Device, string User)> IssueAsync(HttpClient device, string scopes)
    {
        using var response = await device.PostAsync(DevicePath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = Client,
            [OpenIddictConstants.Parameters.Scope] = scopes
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(new Uri(device.BaseAddress!, VerificationPath).AbsoluteUri, body.GetProperty(OpenIddictConstants.Parameters.VerificationUri).GetString());
        Assert.InRange(body.GetProperty(OpenIddictConstants.Parameters.ExpiresIn).GetInt32(), 1, Lifetime);
        return (body.GetProperty(OpenIddictConstants.Parameters.DeviceCode).GetString()!, body.GetProperty(OpenIddictConstants.Parameters.UserCode).GetString()!);
    }

    /// <summary>Follows the real verification form, then submits the enrolled account's password, a fresh factor and its explicit decision.</summary>
    private static async Task DecideAsync(ManagedCacheTestContext test, string code, bool approve, bool disclosure)
    {
        var browser = test.Http.IssuerClient;
        using var verify = await browser.GetAsync(VerificationPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, verify.StatusCode);
        Assert.Equal(UserPath, verify.Headers.Location!.OriginalString);
        var location = await OpenFormAsync(browser, code);
        using var password = await ManagedHumanHttp.PasswordAsync(test, await ManagedHumanHttp.FormAsync(browser, location));
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        using var mfa = await ManagedHumanHttp.MfaAsync(test, await ManagedHumanHttp.FormAsync(browser, password.Headers.Location!));
        Assert.Equal(HttpStatusCode.Redirect, mfa.StatusCode);
        var resumed = await OpenFormAsync(browser, code);
        var consent = await ManagedHumanHttp.FormAsync(browser, resumed, inspect: html => Assert.True(html.Contains(code, StringComparison.Ordinal) && html.Contains(Display, StringComparison.Ordinal) && html.Contains(Client, StringComparison.Ordinal) && html.Contains(ManagedConsentTest.ApiScope, StringComparison.Ordinal) == disclosure));
        using var result = await ManagedHumanHttp.DecideAsync(test, consent, approve);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True((await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(Completion, StringComparison.Ordinal));
    }

    /// <summary>Submits the original issuer form, permitting a same-browser retry without restarting the password or factor phases.</summary>
    private static async Task<Uri> OpenFormAsync(HttpClient browser, string code)
    {
        using var form = await browser.GetAsync(UserPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Equal(StrictOrigin, Assert.Single(form.Headers.GetValues(ReferrerPolicy)));
        var markup = await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var start = await BrowserIssuerHttp.PostAsync(browser, UserPath, new Dictionary<string, string>
        {
            [UserField] = code,
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(markup, AdministratorJourney.Antiforgery)
        });
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.False(start.Headers.Location!.OriginalString.Contains(code, StringComparison.Ordinal));
        return start.Headers.Location;
    }

    /// <summary>Checks the standard OAuth result at the real HTTPS endpoint without retaining the authorization material in assertion output.</summary>
    private static async Task<JsonElement?> PollAsync(HttpClient device, string code, string? error)
    {
        using var response = await device.PostAsync(TokenPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = Client,
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.DeviceCode,
            [OpenIddictConstants.Parameters.DeviceCode] = code
        }), TestContext.Current.CancellationToken);
        Assert.Equal(error is null ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error is null, body.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out var access));
        if (error is not null)
        {
            Assert.Equal(error, body.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
            return null;
        }
        Assert.False(string.IsNullOrEmpty(access.GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()));
        return body;
    }

    /// <summary>Validates both signatures against discovery keys, their distinct audiences and consented identity-claim destinations.</summary>
    private static async Task RequireTokensAsync(ManagedCacheTestContext test, HttpClient device, JsonElement body, string subject, bool disclosure)
    {
        var keys = await ManagedIdentityTokenOracle.ReadKeysAsync(device, device.BaseAddress!);
        foreach (var identity in new[] { true, false })
        {
            var parameters = ManagedTokenValidation.Parameters(device.BaseAddress!, keys, identity ? IdentityType : AccessType);
            parameters.ValidateAudience = identity || disclosure;
            parameters.ValidAudience = identity ? Client : ManagedConsentTest.Resource;
            parameters.LifetimeValidator = (from, to, _, _) => (from is null || from <= test.Http.Now.UtcDateTime) && to > test.Http.Now.UtcDateTime;
            var field = identity ? OpenIddictConstants.Parameters.IdToken : OpenIddictConstants.Parameters.AccessToken;
            var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(body.GetProperty(field).GetString(), parameters);
            Assert.True(result.IsValid, result.Exception?.GetType().Name);
            var token = Assert.IsType<JsonWebToken>(result.SecurityToken);
            Assert.True(token.Subject == subject);
            Assert.True(token.Audiences.ToHashSet(StringComparer.Ordinal).SetEquals(identity ? [Client] : disclosure ? [ManagedConsentTest.Resource] : []));
            Assert.Equal(identity && disclosure, token.TryGetPayloadValue<string>(OpenIddictConstants.Claims.Email, out var address));
            Assert.Equal(identity && disclosure, token.TryGetPayloadValue<bool>(OpenIddictConstants.Claims.EmailVerified, out var verified));
            if (identity && disclosure)
            {
                Assert.True(address == ManagedConsentTest.Address && verified);
            }
        }
    }

    /// <summary>Validates the redeemed token at the real issuer against the enrolled subject and the identity-only scope.</summary>
    private static async Task RequireUserInfoAsync(HttpClient device, string access, string subject, bool disclosure)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoPath);
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        using var response = await device.SendAsync(request, TestContext.Current.CancellationToken);
        await ManagedConsentTest.RequireAsync(response, subject, disclosure ? ManagedConsentTest.Address : null, verified: true);
    }

    /// <summary>Moves only the independent issuer protocol clock by the committed poll interval.</summary>
    private static void Advance(ManagedCacheTestContext test, int seconds = Pace) => test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(seconds);
}
