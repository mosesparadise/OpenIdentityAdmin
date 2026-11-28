using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
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
    private const string InitialDisplay = "Initial display";
    private const string SecretField = "clientSecret";
    private static readonly string[] UnexpectedCallbacks = ["https://device.example/callback"];
    private const string Separator = "/";
    private const string RetirementSuffix = "/retirement";
    private const string GuidFormat = "D";
    private const string RetireInput = """{"reason":"security","confirm":true}""";
    private const string Registration = "/device-registrations";
    private const string Lifecycle = """{"reason":"maintenance","confirm":true}""";
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
    private const int WorkerSeconds = 45;
    private const int WorkerPollMilliseconds = 50;
    private const int Pace = 10;
    private const int AfterBrowserDeadline = 310;
    private const string UserInfoPath = "/connect/userinfo";

    /// <summary>Submits two genuine native codes on the issuer, explicitly approves or refuses each display and observes the public token endpoint.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RegisteredPublicDevice_When_UserApprovesOrDeniesWithFreshMfa_Then_OnlyApprovedDeviceCanRedeemOnce(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var test = await RegisterAsync(http);
        await ManagedConsentTest.SeedAsync(test);
        using var device = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = http.Environment.Profile.Issuer };
        await EditAndToggleAsync(test, device);
        await RequireRetirementAsync(http, device, provider);
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

    /// <summary>Enrols through the browser's protected management route and checks the same identity on the bearer API.</summary>
    private static async Task<ManagedCacheTestContext> RegisterAsync(ApplicationHttpFixture http)
    {
        var input = JsonSerializer.Serialize(new
        {
            clientId = Client,
            displayName = InitialDisplay
        });
        var path = ApplicationHttpFixture.Collection + Registration;
        using var denied = await http.CatalogueAsync(HttpMethod.Post, path, input, direct: false);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var created = await http.CatalogueAsync(HttpMethod.Post, path, input, direct: false, csrf: await http.TokenAsync());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.TryGetProperty(SecretField, out _));
        var details = body.Deserialize<ApplicationDetails>(JsonSerializerOptions.Web)!;
        Assert.Equal(ApplicationEditorRules.DeviceProfile, details.Profile);
        Assert.True(details.Editable && details.Enabled == true);
        Assert.Empty(details.RedirectUris);
        Assert.Null(details.Credential);
        Assert.Null(details.Origins);
        Assert.Equal(ApplicationVersion.Create(details.Id, 1), created.Headers.ETag!.Tag);
        using var duplicate = await http.CatalogueAsync(HttpMethod.Post, ApplicationHttpFixture.ApiCollection + Registration, input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        return new ManagedCacheTestContext(http, details.Id, Client, string.Empty, null);
    }

    /// <summary>Uses application versions to rename, disable and re-enable the registered device before the consent journey.</summary>
    private static async Task EditAndToggleAsync(ManagedCacheTestContext test, HttpClient device)
    {
        const long assigned = 2;
        var edit = JsonSerializer.Serialize(new
        {
            displayName = Display,
            redirectUris = Array.Empty<string>()
        });
        var version = ApplicationVersion.Create(test.Id, assigned);
        using var invalid = await test.Http.UpdateAsync(test.Id, JsonSerializer.Serialize(new
        {
            displayName = Display,
            redirectUris = UnexpectedCallbacks
        }), version);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var renamed = await test.Http.UpdateAsync(test.Id, edit, version);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var stale = await test.Http.UpdateAsync(test.Id, edit, version, direct: true);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var disabled = await test.Http.UpdateAsync(test.Id, Lifecycle, renamed.Headers.ETag!.Tag, enabled: false);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        await RequireUnavailableAsync(device, Client);
        using var enabled = await test.Http.UpdateAsync(test.Id, Lifecycle, disabled.Headers.ETag!.Tag, enabled: true);
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
    }

    /// <summary>Retires a second device through the protected API and checks its retained job, identifier and issuer status.</summary>
    private static async Task RequireRetirementAsync(ApplicationHttpFixture http, HttpClient device, string provider)
    {
        const string retiredClient = "retired-display";
        var input = JsonSerializer.Serialize(new
        {
            clientId = retiredClient,
            displayName = Display
        });
        var path = ApplicationHttpFixture.ApiCollection + Registration;
        using var created = await http.CatalogueAsync(HttpMethod.Post, path, input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var application = await created.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken);
        var detailsPath = ApplicationHttpFixture.ApiCollection + Separator + application!.Id;
        var pending = await IssueAsync(device, Scope, retiredClient);
        using var accepted = await http.CatalogueAsync(HttpMethod.Post, detailsPath + RetirementSuffix, RetireInput,
            created.Headers.ETag!.Tag, key: Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using var job = await http.DirectGetAsync(accepted.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, job.StatusCode);
        using var retained = await http.DirectGetAsync(detailsPath);
        var details = await retained.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken);
        Assert.True(details!.Retired && !details.Editable && details.Enabled == false);
        Assert.NotNull(details.Retirement);
        using var duplicate = await http.CatalogueAsync(HttpMethod.Post, path, input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await RequireUnavailableAsync(device, retiredClient);
        await PollAsync(device, pending.Device, OpenIddictConstants.Errors.InvalidGrant, retiredClient);
        await RequireRevokedAsync(http, application.Id, provider);
    }

    /// <summary>Runs the production worker and observes the original native device pair and its authorization through fresh reads.</summary>
    private static async Task RequireRevokedAsync(ApplicationHttpFixture http, string id, string provider)
    {
        const int pairAndAuthorization = 3;
        var environment = http.Environment;
        var journey = environment.Issuer.Account.Journey;
        var original = (await ApplicationGrantBrowserEvidence.SnapshotAsync(environment)).Where(row => row.Application == id).ToArray();
        Assert.Equal(pairAndAuthorization, original.Length);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(WorkerSeconds));
        await using var worker = GrantWorkerTests.CreateHost(provider, journey.Lease.RuntimeConnection, journey.Clock);
        await worker.StartAsync(deadline.Token);
        while (true)
        {
            var current = (await ApplicationGrantBrowserEvidence.SnapshotAsync(environment)).Where(row => row.Application == id).ToArray();
            Assert.Equal(original.Select(row => row.Id).Order(), current.Select(row => row.Id).Order());
            if (current.All(row => row.Status == OpenIddictConstants.Statuses.Revoked))
            {
                break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(WorkerPollMilliseconds), deadline.Token);
        }
        await worker.StopAsync(deadline.Token);
    }

    /// <summary>Requires the public issuer to withhold device codes after a lifecycle write.</summary>
    private static async Task RequireUnavailableAsync(HttpClient device, string clientId)
    {
        using var result = await device.PostAsync(DevicePath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = clientId,
            [OpenIddictConstants.Parameters.Scope] = Scope
        }), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccessStatusCode);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.TryGetProperty(OpenIddictConstants.Parameters.DeviceCode, out _));
    }

    /// <summary>Uses the actual backchannel endpoint, native nonempty opaque references and the issuer's separate verification URI.</summary>
    private static async Task<(string Device, string User)> IssueAsync(HttpClient device, string scopes, string clientId = Client)
    {
        using var response = await device.PostAsync(DevicePath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = clientId,
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
    private static async Task<JsonElement?> PollAsync(HttpClient device, string code, string? error, string clientId = Client)
    {
        using var response = await device.PostAsync(TokenPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = clientId,
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
