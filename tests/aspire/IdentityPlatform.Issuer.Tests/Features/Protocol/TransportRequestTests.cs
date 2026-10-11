using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class TransportAcceptanceTests
{
    private static readonly string[] MediaCases = ["missing-type", "json", "text", "multipart", "form-suffix"];
    private static readonly string[] Endpoints = ["authorize", "token"];
    private static readonly string[] InvalidTokenMethods = ["GET", "PUT", "DELETE", "HEAD"];
    private static readonly string[] InvalidAuthorizationMethods = ["PUT", "DELETE", "HEAD"];
    private static readonly string[] FormMediaTypes = ["application/x-www-form-urlencoded", "application/x-www-form-urlencoded; charset=utf-8", "Application/X-WWW-Form-Urlencoded; charset=utf-8"];

    public static IEnumerable<TheoryDataRow<string, string, string>> TransportCases =>
        from provider in Providers
        from endpoint in Endpoints
        from variant in (endpoint == "token" ? InvalidTokenMethods : InvalidAuthorizationMethods).Concat(MediaCases)
        select new TheoryDataRow<string, string, string>(provider, endpoint, variant).WithTrait("Provider", provider);

    public static IEnumerable<TheoryDataRow<string, string>> FormCases =>
        from provider in Providers
        from contentType in FormMediaTypes
        select new TheoryDataRow<string, string>(provider, contentType).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(TransportCases))]
    public async Task Protocol_methods_and_media_types_reject_before_issuance(string provider, string endpoint, string variant)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        ExchangeEvidence? code = endpoint == "token" ? await ExchangeEvidence.IssueAsync(environment, browser) : null;
        var flow = code?.Flow ?? environment.RelyingParty.Prepare();
        var values = code is null ? AuthorizationValues(environment, flow) : TokenValues(environment, flow);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(environment.Issuer, "/connect/" + endpoint)) { Content = new FormUrlEncodedContent(values) };
        if (code is not null)
        {
            Assert.True(request.Headers.TryAddWithoutValidation("Authorization", Basic(environment)));
        }
        var reason = "ID2084";
        if (MediaCases.Contains(variant, StringComparer.Ordinal))
        {
            reason = variant == "missing-type" ? "ID2081" : "ID2082";
            var bytes = await request.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            request.Content.Dispose();
            request.Content = new ByteArrayContent(bytes);
            var media = variant switch
            {
                "missing-type" => null,
                "json" => "application/json",
                "text" => "text/plain",
                "multipart" => "multipart/form-data; boundary=synthetic-boundary",
                "form-suffix" => "application/x-www-form-urlencoded-invalid",
                _ => throw new InvalidOperationException("Unknown transport case.")
            };
            request.Content.Headers.ContentType = media is null ? null : MediaTypeHeaderValue.Parse(media);
        }
        else
        {
            request.Method = new HttpMethod(variant);
        }
        if (code is not null)
        {
            await DeniedExchangeAsync(environment, browser, code, request, HttpStatusCode.BadRequest, "invalid_request", reason);
            return;
        }
        var before = await TransportSnapshot.CaptureAsync(environment);
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        await RequireDenialAsync(environment, response, HttpStatusCode.BadRequest, "invalid_request", reason, json: false, head: request.Method == HttpMethod.Head, flow);
        before.RequireUnchanged(await TransportSnapshot.CaptureAsync(environment));
        await RecoverAsync(environment, browser);
    }

    [Theory]
    [MemberData(nameof(FormCases))]
    public async Task Form_POST_authorization_and_token_exchange_preserve_supported_media_parameters(string provider, string contentType)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var navigation = new ProtocolNavigation(environment, "none", contentType);
        using var browser = new HttpClient(navigation) { Timeout = TimeSpan.FromSeconds(15) };
        var flow = await environment.AuthorizeAsync(browser);
        Assert.True(navigation.PostedAuthorization && navigation.CachedRedirect && navigation.CallbackRedirect,
            "Form authorization must use native caching and the registered callback.");
        using var request = TokenRequest(environment, TokenValues(environment, flow), Basic(environment));
        request.Content!.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        await RequireTokensAsync(environment, response, flow);
    }

    private static List<KeyValuePair<string, string>> AuthorizationValues(IssuerEnvironment environment, TestFlow flow) =>
        QueryHelpers.ParseQuery(new Uri(environment.RelyingParty.AuthorizationUri(flow)).Query)
            .SelectMany(value => value.Value.Select(item => new KeyValuePair<string, string>(value.Key, item!))).ToList();

    // Compare only in memory through fixed-message assertions; never print native rows or credentials.
    private sealed class TransportSnapshot(string state)
    {
        public static async Task<TransportSnapshot> CaptureAsync(IssuerEnvironment environment)
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var cancellation = TestContext.Current.CancellationToken;
            var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(value => value.Id)
                .Select(value => new { value.Id, value.Status, value.Type, value.RedemptionDate, value.ExpirationDate }).ToArrayAsync(cancellation);
            var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(value => value.Id)
                .Select(value => new { value.Id, value.Status, value.Subject, value.Scopes }).ToArrayAsync(cancellation);
            var interactions = await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellation);
            var sessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellation);
            var budgets = await database.Set<LoginBrowserBudget>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellation);
            var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellation);
            return new TransportSnapshot(JsonSerializer.Serialize(new
            {
                tokens,
                authorizations,
                interactions,
                sessions,
                budgets,
                audits
            }));
        }

        public void RequireUnchanged(TransportSnapshot after) =>
            Assert.True(state == after.State, "Early protocol rejection must preserve native token/authorization, interaction, session and audit state.");

        private string State => state;
    }
}
